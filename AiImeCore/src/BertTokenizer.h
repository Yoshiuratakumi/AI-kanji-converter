#pragma once
#include <cstdint>
#include <string>
#include <unordered_map>
#include <vector>

/**
 * Character-level BERT tokenizer compatible with cl-tohoku/bert-*-japanese.
 *
 * Japanese (CJK / Hiragana / Katakana) text is split character-by-character.
 * Latin-alphabet tokens are lowercased and treated as single characters
 * (sufficient for Japanese-dominant input; no full WordPiece needed).
 *
 * Sequence layout produced by EncodeWithMasks():
 *   [CLS]  <context tokens, truncated to fit>  [MASK]×N  [SEP]
 */
class BertTokenizer
{
public:
    static constexpr int64_t PAD_ID  = 0;
    static constexpr int64_t UNK_ID  = 100;
    static constexpr int64_t CLS_ID  = 101;
    static constexpr int64_t SEP_ID  = 102;
    static constexpr int64_t MASK_ID = 103;
    static constexpr int     MAX_SEQ_LEN = 128;

    struct Encoding {
        std::vector<int64_t> inputIds;
        std::vector<int64_t> attentionMask;
        std::vector<int64_t> tokenTypeIds;
        std::vector<int>     maskPositions;  ///< 0-based positions of [MASK] tokens
    };

    /** Load vocabulary from vocab.txt (one token per line, index = line number). */
    bool Load(const std::wstring& vocabPath);

    /**
     * Tokenize context and append maskCount [MASK] tokens.
     * Context is truncated from the front so the most recent text is preserved.
     */
    Encoding EncodeWithMasks(const std::wstring& context, int maskCount) const;

    /** Tokenize a single word into token IDs (no special tokens). */
    std::vector<int64_t> TokenizeWord(const std::wstring& word) const;

    int GetVocabSize() const { return static_cast<int>(vocab_.size()); }

private:
    std::unordered_map<std::string, int64_t> vocab_;

    static std::string          WideToUtf8(const std::wstring& w);
    std::vector<std::string>    Tokenize(const std::wstring& text) const;
    static bool                 IsCjkOrKana(uint32_t cp);
    int64_t                     LookupToken(const std::string& tok) const;
};
