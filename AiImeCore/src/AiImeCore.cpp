#include "../include/AiImeCore.h"
#include "Reranker.h"
#include "Dictionary.h"

#include <algorithm>
#include <filesystem>
#include <memory>
#include <numeric>
#include <string>
#include <vector>

// ── Module-level state ────────────────────────────────────────────────────────

namespace {

std::unique_ptr<Reranker>   g_reranker;
std::unique_ptr<Dictionary> g_dictionary;
std::string                 g_lastError;

// Storage for string pointers returned via AIC_LookupDictionary.
// Lifetime: valid until next AIC_LookupDictionary call or AIC_Shutdown().
std::vector<std::wstring>   g_candidateStore;
std::vector<const wchar_t*> g_candidatePtrs;

void SetError(const std::string& msg) { g_lastError = msg; }

}  // namespace

// ── AIC_Initialize ────────────────────────────────────────────────────────────

int AIC_Initialize(const wchar_t* modelDir)
{
    namespace fs = std::filesystem;

    if (!modelDir) {
        SetError("modelDir is null");
        return -1;
    }

    std::wstring dir(modelDir);
    std::wstring modelPath = dir + L"\\bert_mlm.onnx";
    std::wstring vocabPath = dir + L"\\vocab.txt";
    std::wstring dictPath  = dir + L"\\dict.skk";

    g_reranker   = std::make_unique<Reranker>();
    g_dictionary = std::make_unique<Dictionary>();

    if (!g_reranker->Initialize(modelPath, vocabPath)) {
        SetError(g_reranker->LastError());
        g_reranker.reset();
        return -2;
    }

    // Dictionary is optional; failure is non-fatal.
    if (fs::exists(dictPath)) {
        if (!g_dictionary->Load(dictPath))
            SetError("Dictionary load failed (dict.skk); continuing without it.");
    }

    return 0;
}

// ── AIC_Rerank ────────────────────────────────────────────────────────────────

int AIC_Rerank(
    const wchar_t*        context,
    const wchar_t*        hiragana,
    const wchar_t* const* candidates,
    int                   candidateCount,
    int*                  outRankedIndices)
{
    if (!g_reranker) {
        SetError("Engine not initialized. Call AIC_Initialize() first.");
        return -1;
    }
    if (candidateCount <= 0 || !candidates || !outRankedIndices) {
        SetError("Invalid arguments to AIC_Rerank.");
        return -1;
    }
    if (candidateCount > AIC_MAX_CANDIDATES)
        candidateCount = AIC_MAX_CANDIDATES;

    std::wstring ctx(context ? context : L"");

    std::vector<std::wstring> cands;
    cands.reserve(candidateCount);
    for (int i = 0; i < candidateCount; ++i)
        cands.push_back(candidates[i] ? candidates[i] : L"");

    auto scores = g_reranker->ScoreCandidates(ctx, cands);

    // Fill outRankedIndices with 0..N-1, then sort by score descending.
    std::iota(outRankedIndices, outRankedIndices + candidateCount, 0);
    std::stable_sort(
        outRankedIndices, outRankedIndices + candidateCount,
        [&scores](int a, int b) { return scores[a] > scores[b]; });

    if (!g_reranker->LastError().empty())
        SetError(g_reranker->LastError());

    return candidateCount;
}

// ── AIC_LookupDictionary ──────────────────────────────────────────────────────

int AIC_LookupDictionary(const wchar_t* hiragana, const wchar_t** outCandidates)
{
    if (!g_dictionary || !hiragana || !outCandidates) {
        SetError("Invalid arguments to AIC_LookupDictionary.");
        return -1;
    }

    g_candidateStore = g_dictionary->Lookup(hiragana, AIC_MAX_CANDIDATES);
    g_candidatePtrs.clear();
    for (const auto& s : g_candidateStore)
        g_candidatePtrs.push_back(s.c_str());

    int count = static_cast<int>(g_candidatePtrs.size());
    for (int i = 0; i < count; ++i)
        outCandidates[i] = g_candidatePtrs[i];

    return count;
}

// ── AIC_Shutdown ──────────────────────────────────────────────────────────────

void AIC_Shutdown()
{
    g_reranker.reset();
    g_dictionary.reset();
    g_candidateStore.clear();
    g_candidatePtrs.clear();
    g_lastError.clear();
}

// ── AIC_GetLastError ──────────────────────────────────────────────────────────

const char* AIC_GetLastError()
{
    return g_lastError.c_str();
}
