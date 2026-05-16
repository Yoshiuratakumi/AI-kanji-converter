#include "BertTokenizer.h"

#include <algorithm>
#include <cassert>
#include <fstream>
#include <stdexcept>
#include <windows.h>

// ── UTF-8 helpers ─────────────────────────────────────────────────────────────

/*static*/ std::string BertTokenizer::WideToUtf8(const std::wstring& w)
{
    if (w.empty()) return {};
    int n = WideCharToMultiByte(CP_UTF8, 0, w.data(), static_cast<int>(w.size()),
                                nullptr, 0, nullptr, nullptr);
    std::string s(n, '\0');
    WideCharToMultiByte(CP_UTF8, 0, w.data(), static_cast<int>(w.size()),
                        s.data(), n, nullptr, nullptr);
    return s;
}

// Decode one UTF-8 codepoint; advance p past it.
static uint32_t NextCodepoint(const char*& p, const char* end)
{
    if (p >= end) return 0;
    auto c = static_cast<uint8_t>(*p++);
    uint32_t cp;
    int extra;
    if      (c < 0x80) { cp = c;        extra = 0; }
    else if (c < 0xE0) { cp = c & 0x1F; extra = 1; }
    else if (c < 0xF0) { cp = c & 0x0F; extra = 2; }
    else               { cp = c & 0x07; extra = 3; }
    while (extra-- > 0 && p < end)
        cp = (cp << 6) | (static_cast<uint8_t>(*p++) & 0x3F);
    return cp;
}

// Advance p to the start of the next UTF-8 character.
static const char* NextCharBoundary(const char* p, const char* end)
{
    if (p >= end) return end;
    auto c = static_cast<uint8_t>(*p++);
    if      (c < 0x80) return p;
    else if (c < 0xE0) return std::min(p + 1, end);
    else if (c < 0xF0) return std::min(p + 2, end);
    else               return std::min(p + 3, end);
}

// ── Character classification ──────────────────────────────────────────────────

/*static*/ bool BertTokenizer::IsCjkOrKana(uint32_t cp)
{
    return (cp >= 0x4E00 && cp <= 0x9FFF)   ||  // CJK Unified Ideographs (basic)
           (cp >= 0x3400 && cp <= 0x4DBF)   ||  // CJK Extension A
           (cp >= 0x20000 && cp <= 0x2A6DF) ||  // CJK Extension B
           (cp >= 0xF900 && cp <= 0xFAFF)   ||  // CJK Compatibility Ideographs
           (cp >= 0x3040 && cp <= 0x309F)   ||  // Hiragana
           (cp >= 0x30A0 && cp <= 0x30FF)   ||  // Katakana
           (cp >= 0x31F0 && cp <= 0x31FF)   ||  // Katakana Phonetic Extensions
           (cp >= 0xFF65 && cp <= 0xFF9F)   ||  // Halfwidth Katakana
           (cp >= 0xFF00 && cp <= 0xFF60)   ||  // Fullwidth Latin / symbols
           (cp >= 0x2E80 && cp <= 0x2FFF)   ||  // CJK Radicals / Kangxi
           (cp >= 0x3000 && cp <= 0x303F);      // CJK Symbols and Punctuation
}

// ── Tokenization ──────────────────────────────────────────────────────────────

std::vector<std::string> BertTokenizer::Tokenize(const std::wstring& text) const
{
    std::string utf8 = WideToUtf8(text);
    const char* p   = utf8.data();
    const char* end = p + utf8.size();

    std::vector<std::string> tokens;
    tokens.reserve(utf8.size());

    while (p < end) {
        const char* next = NextCharBoundary(p, end);

        // Decode codepoint for classification (we need to re-decode since
        // NextCharBoundary doesn't return the codepoint).
        const char* tmp = p;
        uint32_t cp = NextCodepoint(tmp, end);

        if (cp == ' ' || cp == '\t' || cp == '\n' || cp == '\r') {
            // Whitespace: word boundary, skip
        } else if (IsCjkOrKana(cp)) {
            // Each CJK / kana character is its own token
            tokens.emplace_back(p, next);
        } else {
            // ASCII / Latin: lowercase single character
            std::string ch(p, next);
            if (cp >= 'A' && cp <= 'Z')
                ch[0] = static_cast<char>(ch[0] - 'A' + 'a');
            tokens.push_back(std::move(ch));
        }

        p = next;
    }

    return tokens;
}

// ── Vocabulary loading ────────────────────────────────────────────────────────

bool BertTokenizer::Load(const std::wstring& vocabPath)
{
    // Use _wfopen so non-ANSI paths (e.g. Japanese directory names) work on MinGW.
    FILE* fp = _wfopen(vocabPath.c_str(), L"r");
    if (!fp) return false;

    vocab_.clear();
    char buf[4096];
    int64_t idx = 0;
    while (fgets(buf, sizeof(buf), fp)) {
        std::string line(buf);
        while (!line.empty() && (line.back() == '\n' || line.back() == '\r'))
            line.pop_back();
        vocab_[line] = idx++;
    }
    fclose(fp);
    return !vocab_.empty();
}

int64_t BertTokenizer::LookupToken(const std::string& tok) const
{
    auto it = vocab_.find(tok);
    return (it != vocab_.end()) ? it->second : UNK_ID;
}

// ── Public encode / tokenize ──────────────────────────────────────────────────

BertTokenizer::Encoding BertTokenizer::EncodeWithMasks(
    const std::wstring& context, int maskCount) const
{
    auto contextToks = Tokenize(context);

    // Budget: [CLS] + context + [MASK]*maskCount + [SEP]
    int available = MAX_SEQ_LEN - 2 - maskCount;
    if (available < 0) available = 0;

    // Truncate oldest (front) tokens to keep the most recent context
    if (static_cast<int>(contextToks.size()) > available) {
        contextToks.erase(
            contextToks.begin(),
            contextToks.begin() + (static_cast<int>(contextToks.size()) - available));
    }

    Encoding enc;
    enc.inputIds.reserve(MAX_SEQ_LEN);

    // [CLS]
    enc.inputIds.push_back(CLS_ID);

    // Context
    for (const auto& tok : contextToks)
        enc.inputIds.push_back(LookupToken(tok));

    // [MASK] × maskCount
    for (int i = 0; i < maskCount; ++i) {
        enc.maskPositions.push_back(static_cast<int>(enc.inputIds.size()));
        enc.inputIds.push_back(MASK_ID);
    }

    // [SEP]
    enc.inputIds.push_back(SEP_ID);

    int seqLen = static_cast<int>(enc.inputIds.size());
    enc.attentionMask.assign(seqLen, 1);
    enc.tokenTypeIds.assign(seqLen, 0);

    return enc;
}

std::vector<int64_t> BertTokenizer::TokenizeWord(const std::wstring& word) const
{
    auto toks = Tokenize(word);
    std::vector<int64_t> ids;
    ids.reserve(toks.size());
    for (const auto& t : toks)
        ids.push_back(LookupToken(t));
    return ids;
}
