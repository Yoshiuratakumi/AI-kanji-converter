#include "Reranker.h"

#include <algorithm>
#include <cmath>
#include <map>
#include <numeric>
#include <stdexcept>

#include <onnxruntime_cxx_api.h>

// ── PIMPL struct ──────────────────────────────────────────────────────────────

struct Reranker::OrtState
{
    Ort::Env            env;
    Ort::SessionOptions sessionOpts;
    Ort::Session        session;

    // Construct with a loaded model
    OrtState(const std::wstring& modelPath)
        : env(ORT_LOGGING_LEVEL_WARNING, "AiImeCore")
        , session(nullptr)
    {
        sessionOpts.SetIntraOpNumThreads(2);
        sessionOpts.SetGraphOptimizationLevel(
            GraphOptimizationLevel::ORT_ENABLE_EXTENDED);
        // Prefer DirectML (GPU) when available; falls back to CPU silently
        // (We keep it CPU-only for maximum compatibility; remove the comment
        //  and add OrtSessionOptionsAppendExecutionProvider_DML() to opt-in.)
        session = Ort::Session(env, modelPath.c_str(), sessionOpts);
    }
};

// ── Construction / destruction ────────────────────────────────────────────────

Reranker::Reranker() = default;
Reranker::~Reranker() = default;

bool Reranker::Initialize(const std::wstring& modelPath, const std::wstring& vocabPath)
{
    if (!tokenizer_.Load(vocabPath)) {
        lastError_ = "Failed to load vocabulary";
        return false;
    }
    try {
        ort_ = std::make_unique<OrtState>(modelPath);
    } catch (const Ort::Exception& e) {
        lastError_ = e.what();
        return false;
    }
    return true;
}

// ── Math helper ───────────────────────────────────────────────────────────────

/*static*/ std::vector<float> Reranker::LogSoftmax(const float* logits, int size)
{
    // Numerically stable log-softmax
    float maxVal = *std::max_element(logits, logits + size);
    float sumExp = 0.0f;
    for (int i = 0; i < size; ++i)
        sumExp += std::exp(logits[i] - maxVal);
    float logSumExp = maxVal + std::log(sumExp + 1e-9f);

    std::vector<float> out(size);
    for (int i = 0; i < size; ++i)
        out[i] = logits[i] - logSumExp;
    return out;
}

// ── ONNX Runtime inference ────────────────────────────────────────────────────

std::vector<float> Reranker::RunInference(const BertTokenizer::Encoding& enc)
{
    if (!ort_) throw std::runtime_error("Reranker not initialized");

    int seqLen = static_cast<int>(enc.inputIds.size());
    std::vector<int64_t> shape = {1, seqLen};

    auto memInfo = Ort::MemoryInfo::CreateCpu(OrtArenaAllocator, OrtMemTypeDefault);

    // ONNX Runtime requires non-const pointers for CreateTensor, so we copy.
    std::vector<int64_t> ids    = enc.inputIds;
    std::vector<int64_t> mask   = enc.attentionMask;
    std::vector<int64_t> typeIds= enc.tokenTypeIds;

    std::vector<Ort::Value> inputs;
    inputs.push_back(Ort::Value::CreateTensor<int64_t>(
        memInfo, ids.data(),     ids.size(),     shape.data(), 2));
    inputs.push_back(Ort::Value::CreateTensor<int64_t>(
        memInfo, mask.data(),    mask.size(),    shape.data(), 2));
    inputs.push_back(Ort::Value::CreateTensor<int64_t>(
        memInfo, typeIds.data(), typeIds.size(), shape.data(), 2));

    const char* inputNames[]  = {"input_ids", "attention_mask", "token_type_ids"};
    const char* outputNames[] = {"logits"};

    auto outputs = ort_->session.Run(
        Ort::RunOptions{nullptr},
        inputNames,  inputs.data(),  3,
        outputNames, 1);

    // Output: [1, seqLen, vocabSize]
    const float* ptr = outputs[0].GetTensorData<float>();
    auto dims = outputs[0].GetTensorTypeAndShapeInfo().GetShape();
    int64_t total = dims[0] * dims[1] * dims[2];

    return std::vector<float>(ptr, ptr + total);
}

// ── Public API ────────────────────────────────────────────────────────────────

std::vector<float> Reranker::ScoreCandidates(
    const std::wstring&              context,
    const std::vector<std::wstring>& candidates)
{
    int n = static_cast<int>(candidates.size());
    std::vector<float> scores(n, -1e30f);

    if (!ort_) {
        lastError_ = "Reranker not initialized";
        return scores;
    }

    // Tokenize all candidates; group by token count for batched inference.
    struct CandInfo {
        int                  originalIdx;
        std::vector<int64_t> tokenIds;
    };
    std::map<int, std::vector<CandInfo>> byLen;

    for (int i = 0; i < n; ++i) {
        auto ids = tokenizer_.TokenizeWord(candidates[i]);
        if (!ids.empty())
            byLen[static_cast<int>(ids.size())].push_back({i, std::move(ids)});
    }

    // One BERT forward pass per unique candidate length
    for (auto& [maskCount, group] : byLen) {
        try {
            BertTokenizer::Encoding enc =
                tokenizer_.EncodeWithMasks(context, maskCount);

            if (static_cast<int>(enc.maskPositions.size()) != maskCount)
                continue; // sequence too long to fit any masks

            auto logits = RunInference(enc);

            int seqLen   = static_cast<int>(enc.inputIds.size());
            int vocabSize = static_cast<int>(logits.size()) / seqLen;

            // Pre-compute log-softmax at each [MASK] position
            std::vector<std::vector<float>> logProbs(maskCount);
            for (int m = 0; m < maskCount; ++m) {
                int pos = enc.maskPositions[m];
                logProbs[m] = LogSoftmax(logits.data() + pos * vocabSize, vocabSize);
            }

            // Score each candidate: sum of log P(char_i) at position i
            for (const auto& ci : group) {
                float score = 0.0f;
                for (int m = 0; m < maskCount; ++m) {
                    int64_t tid = ci.tokenIds[m];
                    if (tid >= 0 && tid < vocabSize)
                        score += logProbs[m][static_cast<int>(tid)];
                }
                scores[ci.originalIdx] = score;
            }

        } catch (const Ort::Exception& e) {
            lastError_ = e.what();
            // Continue with remaining groups; caller gets partial scores.
        }
    }

    return scores;
}
