# AiIME 設計書

## 1. システム概要

Windows 11 (64bit) 上で動作する、文脈認識型 AI 日本語 IME。

- **AI推論はローカル完結** — 文章内容をクラウドへ送らず機密性を保持
- **変換方式**: ローマ字 → ひらがな → 辞書引き（最大 16 候補）→ BERT MLM リランク → 上位候補を表示
- **対象アプリ**: Word / PowerPoint / Excel などの TSF 対応 Windows アプリ全般

---

## 2. コンポーネント構成

```
[ユーザーキー入力]
       │ ローマ字入力
       ▼
┌─────────────────────────────────────────┐
│  C# IMEシェル (AiImeShell.comhost.dll)  │  ← Phase 3 完成・登録済
│  · TextInputProcessor (ITfTextInputProcessor)
│  · KeyEventSink (ITfKeyEventSink)        │
│  · CompositionManager (TSF編集セッション)│
│  · RomajiConverter (ローマ字→ひらがな)  │
│  · CandidateWindow (WinForms浮動ウィンドウ)
└────────────┬────────────────────────────┘
             │ P/Invoke (NativeLibrary事前ロード)
             ▼
┌─────────────────────────────────────────┐
│  C++ DLL (AiImeCore.dll)                │  ← Phase 1・2 完成
│  · Dictionary  : SKK 辞書引き           │
│  · BertTokenizer: 文字単位トークナイズ  │
│  · Reranker    : BERT MLM (ONNX Runtime)│
└─────────────────────────────────────────┘
       │ ファイル読込
       ▼
┌─────────────────────────────────────────┐
│  models/ (実行時データ)                  │
│  · bert_mlm.onnx  (546 MB)              │  ← cl-tohoku/bert-base-japanese-v3
│  · vocab.txt      (263 KB, 32K tokens)  │
│  · dict.skk       (6.3 MB, SKK-JISYO.L)│
└─────────────────────────────────────────┘
```

---

## 3. 処理フロー

| # | 担当 | 処理 | 目標時間 |
|---|------|------|---------|
| ① | C# RomajiConverter | ローマ字キーをひらがなに変換（ステートマシン） | < 0.1 ms |
| ② | C# TSF | スペースキー打鍵を検知、TSF 編集セッションを開始 | 1–2 ms |
| ③ | C# P/Invoke | 「前文脈 (最大200字)」と「ひらがな」を DLL へ渡す | < 0.1 ms |
| ④ | C++ Dictionary | SKK 辞書からひらがな → 漢字候補 (最大 `AIC_MAX_CANDIDATES=16`) | 1–3 ms |
| ⑤ | C++ Reranker | BERT MLM で候補リランク (ONNX Runtime) | 10–20 ms |
| ⑥ | C++ → C# | スコア順インデックス配列を返す | < 0.1 ms |
| ⑦ | C# TSF | CandidateWindow でリスト表示、Enter/数字キーで確定 | 2–5 ms |

**合計目安: 14–30 ms**（ボトルネックはステップ⑤のみ）

---

## 4. C++ DLL — AiImeCore.dll（完成）

### 4.1 公開 API

```c
/* include/AiImeCore.h */

// モデルディレクトリを指定して初期化（bert_mlm.onnx / vocab.txt / dict.skk）
int  AIC_Initialize(const wchar_t* modelDir);

// BERT MLM で候補をリランク。outRankedIndices にスコア降順の元インデックスを格納
int  AIC_Rerank(const wchar_t*        context,
                const wchar_t*        hiragana,
                const wchar_t* const* candidates,
                int                   candidateCount,
                int*                  outRankedIndices);

// 辞書からひらがな → 漢字候補を取得（ポインタは次の呼び出しまで有効）
int  AIC_LookupDictionary(const wchar_t* hiragana, const wchar_t** outCandidates);

void        AIC_Shutdown();
const char* AIC_GetLastError();   // UTF-8
#define AIC_MAX_CANDIDATES 16
```

### 4.2 モジュール一覧

| ファイル | クラス | 役割 | 状態 |
|---------|--------|------|------|
| `dllmain.cpp` | — | DLL エントリポイント | ✅ |
| `AiImeCore.cpp` | — | 公開 API の実装・モジュール連携 | ✅ |
| `Reranker.cpp/h` | `Reranker` | ONNX Runtime 推論・MLM スコアリング | ✅ |
| `BertTokenizer.cpp/h` | `BertTokenizer` | 文字単位 BERT トークナイザー | ✅ |
| `Dictionary.cpp/h` | `Dictionary` | SKK 形式辞書の読込・検索 | ✅ |

### 4.3 BERT MLM スコアリング（アルゴリズム）

**入力フォーマット:**
```
[CLS] <前文脈 (末尾 MAX_SEQ_LEN-3-N トークンに切り詰め)> [MASK]×N [SEP]
```
N = 変換候補の文字数

**スコア計算:**
```
score(候補) = Σ log P(char_i | context)  for i in 1..N
```
各 `[MASK]` 位置で log-softmax を計算し、候補文字トークンの対数確率を合計する。

**最適化:**
同じ文字数（= N が等しい）の候補は 1 回の forward pass を共有する。
同音異義語は通常同じ読み長さ → **典型的なケースは 1 forward pass のみ**。

### 4.4 CMake ビルド手順（実施済）

```bash
# MinGW GCC + CMake を使用
cmake -S . -B build -G "MinGW Makefiles" -DONNXRUNTIME_ROOT=C:/onnxruntime-win-x64-1.20.1
cmake --build build --config Release
# → build/bin/Release/libAiImeCore.dll が生成される
# → AiImeShell publish フォルダへ AiImeCore.dll としてコピー
```

---

## 5. 辞書・モデルデータ（調達済）

| データ | 入手元 | サイズ | 状態 |
|--------|--------|--------|------|
| `bert_mlm.onnx` | cl-tohoku/bert-base-japanese-v3 を ONNX エクスポート | 546 MB | ✅ |
| `vocab.txt` | 同上（語彙ファイル） | 263 KB (32K tokens) | ✅ |
| `dict.skk` | SKK-JISYO.L (skk-dev/dict) | 6.3 MB (~120K語) | ✅ |

### モデルエクスポート手順（実施済）

```bash
pip install transformers torch onnx onnxruntime
python tools/export_model.py --output-dir models/
```

### INT8 量子化（オプション・推論約 40% 高速化）

```python
from onnxruntime.quantization import quantize_dynamic, QuantType
quantize_dynamic("models/bert_mlm.onnx",
                 "models/bert_mlm_int8.onnx",
                 weight_type=QuantType.QInt8)
```

量子化後は `AIC_Initialize` に渡すパスを `bert_mlm_int8.onnx` に変更するだけで機能する。

---

## 6. C# IMEシェル — AiImeShell（完成・登録済）

### 6.1 プロジェクト構成（実際のファイル）

```
AiImeShell/
├── AiImeShell.csproj              .NET 8.0-windows / EnableComHosting / x64
└── src/
    ├── Guids.cs                   CLSID / LangProfile GUID 定数
    ├── TextInputProcessor.cs      AiImeTextService : ITfTextInputProcessor  [ComVisible]
    ├── KeyEventSink.cs            KeyEventSink : ITfKeyEventSink, ITfCompositionSink
    ├── CompositionManager.cs      TSF 編集セッション管理 + LambdaEditSession
    ├── CandidateWindow.cs         WinForms 浮動ウィンドウ (WS_EX_NOACTIVATE)
    ├── RomajiTable.cs             RomajiConverter ローマ字→ひらがな変換テーブル
    ├── AiImeCoreBridge.cs         P/Invoke ブリッジ (NativeLibrary 事前ロード)
    └── Interop/
        ├── TsfInterop.cs          TSF COM インターフェース定義 (全 public)
        └── NativeMethods.cs       Win32 P/Invoke (GetCaretPos 等)

tools/
├── Register.ps1                   COM + TSF レジストリ登録スクリプト
└── Unregister.ps1                 登録解除スクリプト
```

### 6.2 実装した TSF COM インターフェース

| インターフェース | IID | 役割 | 実装者 |
|----------------|-----|------|--------|
| `ITfTextInputProcessor` | AA80E7F7-... | IME メインエントリポイント (Activate/Deactivate) | AiImeTextService |
| `ITfKeyEventSink` | AA80E7F5-... | キーイベント処理 | KeyEventSink |
| `ITfCompositionSink` | A781718C-... | コンポジション終了通知 | KeyEventSink |
| `ITfEditSession` | AA80E803-... | TSF 編集セッション (lambda ラッパー) | LambdaEditSession |

COM から QI されるインターフェース（TSF 側が実装）:

| インターフェース | IID | 用途 |
|----------------|-----|------|
| `ITfThreadMgr` | AA80E801-... | キーストロークマネージャ取得 |
| `ITfKeystrokeMgr` | AA80E7F0-... | AdviseKeyEventSink 登録 |
| `ITfInsertAtSelection` | 55CE16BA-... | カーソル位置へテキスト挿入 |
| `ITfContextComposition` | D40C8AAE-AC92-4FC7-... | コンポジション開始 |
| `ITfComposition` | 20168D64-... | コンポジションテキスト更新 |

### 6.3 IME ステートマシン

```
Idle ──[ローマ字キー]──→ Composing ──[Space]──→ Converting
  ↑                          │   ↑                  │   ↑
  └──[Escape/全削除]──────────┘   └──[Escape]────────┘   │
  └──[Enter]────────(ひらがなのまま確定)                   │
  └──[Enter/数字1-9]───────────────────────────────────────┘
                                                    (漢字確定)
```

### 6.4 GUID 一覧

```
CLSID (COM/TSF 登録):   {C7E9D1A0-B2F3-4E56-A789-0C1D2E3F4A5B}
LangProfile:            {D8F0E2B1-C3A4-5F67-B890-1D2E3F4A5B6C}
Language:               0x0411 (日本語)
IME名:                  AI日本語IME
```

### 6.5 ビルド・配置（実施済）

```powershell
# ビルド
$dotnet = "$env:LOCALAPPDATA\dotnet8sdk\dotnet.exe"
& $dotnet publish AiImeShell.csproj -c Release -r win-x64 --self-contained false `
    -o bin\Release\net8.0-windows\publish

# 生成物 (publish フォルダ)
# AiImeShell.comhost.dll  ← TSF が COM で読み込む native ホスト
# AiImeShell.dll          ← マネージドアセンブリ
# AiImeShell.runtimeconfig.json

# AiImeCore.dll / onnxruntime.dll / models/ を手動コピー後:
& tools\Register.ps1     # 管理者権限で実行
```

---

## 7. レジストリ登録（実施済）

```
HKLM\SOFTWARE\Classes\CLSID\{C7E9D1A0-B2F3-4E56-A789-0C1D2E3F4A5B}
  (Default) = "AI日本語IME"
  InProcServer32\
    (Default)      = "C:\Users\...\AiImeShell\bin\Release\net8.0-windows\publish\AiImeShell.comhost.dll"
    ThreadingModel = "Apartment"

HKLM\SOFTWARE\Microsoft\CTF\TIP\{C7E9D1A0-...}\LanguageProfile\0x0411\{D8F0E2B1-...}
  Description = "AI日本語IME"
  Enable      = 1
  Display     = 0
```

---

## 8. ディレクトリ構成（現状）

```
C:\Users\...\AiImeCore\              ← C++ DLL プロジェクト
├── CMakeLists.txt
├── DESIGN.md                        ← 本ドキュメント
├── include/AiImeCore.h
├── src/
│   ├── dllmain.cpp
│   ├── AiImeCore.cpp
│   ├── Reranker.cpp / .h
│   ├── BertTokenizer.cpp / .h
│   └── Dictionary.cpp / .h
├── build/bin/Release/
│   ├── libAiImeCore.dll             ← MinGW ビルド成果物
│   └── onnxruntime.dll
├── models/                          ← 実行時データ（マスター）
│   ├── bert_mlm.onnx  (546 MB)
│   ├── vocab.txt      (263 KB)
│   └── dict.skk       (6.3 MB)
└── tools/
    └── export_model.py

C:\Users\...\AiImeShell\             ← C# TSF シェル プロジェクト
├── AiImeShell.csproj
├── src/                             ← 上記 6.1 参照
├── tools/
│   ├── Register.ps1
│   └── Unregister.ps1
└── bin\Release\net8.0-windows\publish\   ← 配置済み実行ファイル
    ├── AiImeShell.comhost.dll  (194 KB)   ← COM ホスト
    ├── AiImeShell.dll          (30 KB)    ← マネージドアセンブリ
    ├── AiImeCore.dll           (3 MB)     ← C++ コア (libAiImeCore.dll をリネーム)
    ├── onnxruntime.dll         (11 MB)
    └── models/
        ├── bert_mlm.onnx  (546 MB)
        ├── vocab.txt      (263 KB)
        └── dict.skk       (6.3 MB)
```

---

## 9. 開発環境

| ツール | 用途 | 実際の環境 |
|--------|------|-----------|
| MinGW GCC (MSYS2 mingw64) | C++ DLL ビルド | CMake + MinGW Makefiles |
| CMake ≥ 3.20 | C++ ビルドシステム | 3.23.3 |
| ONNX Runtime 1.20.1 | 推論エンジン（DLL 同梱配布） | GitHub Releases |
| .NET 8 SDK | C# ビルド | `%LOCALAPPDATA%\dotnet8sdk\` (8.0.421) |
| .NET 8 Runtime | C# 実行環境 | `C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\8.0.8` |
| Python 3.x | モデルエクスポート（開発時のみ） | transformers / torch / onnx |

---

## 10. 実装フェーズ

| フェーズ | 内容 | 状態 |
|---------|------|------|
| Phase 1 | C++ DLL コア実装（Dictionary / Reranker / Tokenizer / API） | ✅ 完成 |
| Phase 2 | モデルエクスポート + 辞書調達 + DLL ビルド | ✅ 完成 |
| Phase 3 | C# TSF シェル実装・COM 登録・配置 | ✅ 完成・登録済 |
| Phase 4 | 統合テスト（Word / Notepad 等での実動作確認） | 🔲 次フェーズ |
| Phase 5 | INT8 量子化チューニング・パフォーマンス計測 | 🔲 未着手 |
| Phase 6 | インストーラ整備（配布パスを `C:\Program Files\AiIME\` に固定） | 🔲 未着手 |

---

## 11. テスト手順（Phase 4 に向けて）

1. **ログオフ → ログオン**（既存プロセスの DLL ロックを解放するため）
2. タスクバーの言語バーで `Win+Space` → **「AI日本語IME」** を選択
3. メモ帳を開き、ローマ字入力（例: `nihongo`）→ スペースキーで変換
4. 候補ウィンドウが表示され、Enter/数字キーで確定できることを確認
5. Escape キーでひらがな表示に戻ることを確認

**初回起動時の注意:** BERT モデル (546 MB) のロードに数秒かかる。

---

## 12. 既知の制約・今後の課題

| 項目 | 内容 |
|------|------|
| モデルサイズ | bert_mlm.onnx が 546 MB あり、IME 有効化時に全量メモリ展開される |
| 初回遅延 | Activate 時にモデルをロードするため数秒の遅延が発生する |
| 配置パス | 現在はユーザーホーム内の開発パスに配置（Phase 6 で `Program Files` へ移行予定） |
| DLL 更新 | 配置済み DLL を更新する際はロックを避けるためログオフが必要（またはリネーム置換） |
| 文脈取得 | 現在は確定済みテキストを内部バッファで管理（最大 200 字）。TSF の GetText は未使用 |
