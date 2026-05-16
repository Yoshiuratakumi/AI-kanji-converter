#pragma once
#include <string>
#include <unordered_map>
#include <vector>

/**
 * Hiragana → kanji dictionary backed by an SKK dictionary file.
 *
 * SKK format (UTF-8):
 *   ;; comment line
 *   ひらがな /候補1/候補2/候補3;注記/.../
 *
 * Large freely available dictionaries (recommended):
 *   https://github.com/skk-dev/dict  (SKK-JISYO.L, ~120 000 entries)
 *
 * The dictionary is optional; the C# TSF layer can supply its own candidates
 * and use the DLL only for reranking (AIC_Rerank).
 */
class Dictionary
{
public:
    /** Load from an SKK-format dictionary file (UTF-8 encoded). */
    bool Load(const std::wstring& dictPath);

    /** Look up candidates for a hiragana string. Returns up to maxCount. */
    std::vector<std::wstring> Lookup(const std::wstring& hiragana,
                                     int maxCount = 16) const;

    bool IsLoaded() const { return !dict_.empty(); }
    size_t EntryCount() const { return dict_.size(); }

private:
    std::unordered_map<std::wstring, std::vector<std::wstring>> dict_;

    static std::string  WideToUtf8(const std::wstring& w);
    static std::wstring Utf8ToWide(const std::string& s);
};
