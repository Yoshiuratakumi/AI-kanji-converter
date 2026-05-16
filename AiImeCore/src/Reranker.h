#pragma once
#include "BertTokenizer.h"

#include <memory>
#include <string>
#include <vector>

// Forward-declare ONNX Runtime types to avoid exposing onnxruntime headers
// to callers who only include this header.
namespace Ort {
    struct Env;
    struct Session;
    struct SessionOptions;
}

/**
 * Reranks kanji candidates with BERT-MLM (cl-tohoku/bert-small-japanese).
 *
 * Algorithm (one BERT forward pass per unique candidate-length group):
 *   1. Build input:  [CLS] <context, truncated> [MASK]×N [SEP]
 *   2. Run ONNX Runtime → logits [1, seqLen, vocabSize]
 *   3. Apply log-softmax at each [MASK] position.
 *   4. Score candidate = Σ log P(char_i) over its N character tokens.
 *
 * Candidates of the same character length share one forward pass,
 * so the typical case (≤10 homophones, same reading length) costs a single pass.
 */
class Reranker
{
public:
    Reranker();
    ~Reranker();

    /**
     * Load ONNX model and vocabulary.
     * @param modelPath  Path to bert_mlm.onnx
     * @param vocabPath  Path to vocab.txt
     */
    bool Initialize(const std::wstring& modelPath, const std::wstring& vocabPath);

    /**
     * Score each candidate in the given context.
     * Returns a float score per candidate (higher = better fit).
     * Unscored candidates (e.g. zero-length) receive -∞.
     */
    std::vector<float> ScoreCandidates(
        const std::wstring&              context,
        const std::vector<std::wstring>& candidates);

    const std::string& LastError() const { return lastError_; }

private:
    // PIMPL: keeps onnxruntime headers out of Reranker.h
    struct OrtState;
    std::unique_ptr<OrtState> ort_;

    BertTokenizer tokenizer_;
    std::string   lastError_;

    // Run one BERT forward pass; returns flat logits [seqLen × vocabSize].
    std::vector<float> RunInference(const BertTokenizer::Encoding& enc);

    static std::vector<float> LogSoftmax(const float* logits, int size);
};
