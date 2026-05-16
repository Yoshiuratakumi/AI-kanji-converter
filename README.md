# AI 漢字変換 IME (AiIME)

Windows 11 (64bit) 上で動作する、文脈認識 AI 日本語 IME。

- **AI 推論はローカル完結** — 文章内容をクラウドへ送らずプライバシーを保護
- **変換方式**: ローマ字入力 → ひらがな → 辞書引き（最大 16 候補）→ BERT MLM リランク → 上位候補を表示
- **対象アプリ**: Word / PowerPoint / Excel など TSF 対応 Windows アプリ全般

## ディレクトリ構成

```
AI-kanji-converter/
├── AiImeCore/          C++ DLL コア (辞書引き・BERT 推論・トークナイザ)
├── AiImeShell/         C# TSF シェル (キー入力・候補ウィンドウ・COM 登録)
├── AiImeRes/           リソース DLL (アイコン等)
├── AiImeTest/          COM/TSF 疎通テスト用スタンドアロン C プログラム
└── DESIGN.md           システム設計書
```

## セットアップ

設計詳細・ビルド手順・配置手順は **[DESIGN.md](DESIGN.md)** を参照。

### 必要なモデルファイル (Git 管理外 / 手動配置)

| ファイル | 取得元 | 配置先 |
|---------|--------|--------|
| `bert_mlm.onnx` (546 MB) | cl-tohoku/bert-base-japanese-v3 を `tools/export_model.py` でエクスポート | `AiImeCore/models/` |

`vocab.txt` と `dict.skk` はリポジトリに含まれています。

## 開発フェーズ

| Phase | 内容 | 状態 |
|-------|------|------|
| 1 | C++ DLL コア実装 | ✅ 完了 |
| 2 | モデルエクスポート・辞書調整 | ✅ 完了 |
| 3 | C# TSF シェル実装・COM 登録 | ✅ 完了 |
| 4 | 統合テスト (Word / メモ帳 等) | 🔲 進行中 |
| 5 | INT8 量子化・パフォーマンス計測 | 🔲 未着手 |
| 6 | インストーラ整備 | 🔲 未着手 |
