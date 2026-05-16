#pragma once
#include <windows.h>

#ifdef AIIMECORE_EXPORTS
#  define AIIMECORE_API __declspec(dllexport)
#else
#  define AIIMECORE_API __declspec(dllimport)
#endif

#ifdef __cplusplus
extern "C" {
#endif

/**
 * Initialize the AI engine.
 *
 * @param modelDir  Directory containing:
 *                    bert_mlm.onnx  – BERT-MLM model exported via tools/export_model.py
 *                    vocab.txt      – BERT vocabulary (one token per line)
 *                    dict.skk       – SKK-format dictionary (optional)
 * @return  0 on success, negative on failure. Call AIC_GetLastError() for details.
 */
AIIMECORE_API int AIC_Initialize(const wchar_t* modelDir);

/**
 * Rerank kanji candidates using BERT-MLM context scoring.
 *
 * @param context          Previous 3 sentences (null-terminated wide string).
 * @param hiragana         Current hiragana input (null-terminated wide string).
 * @param candidates       Array of candidateCount null-terminated wide strings.
 * @param candidateCount   Number of candidates (max AIC_MAX_CANDIDATES).
 * @param outRankedIndices Caller-allocated int[candidateCount].
 *                         Filled with original indices, best candidate first.
 * @return  Number of ranked candidates, or -1 on error.
 */
AIIMECORE_API int AIC_Rerank(
    const wchar_t*        context,
    const wchar_t*        hiragana,
    const wchar_t* const* candidates,
    int                   candidateCount,
    int*                  outRankedIndices
);

/**
 * Look up kanji candidates from the built-in dictionary.
 *
 * @param hiragana      Input hiragana (null-terminated wide string).
 * @param outCandidates Caller-allocated const wchar_t*[AIC_MAX_CANDIDATES].
 *                      Pointers are valid until the next call or AIC_Shutdown().
 * @return  Number of candidates found, or -1 on error.
 */
AIIMECORE_API int AIC_LookupDictionary(
    const wchar_t*  hiragana,
    const wchar_t** outCandidates
);

/** Release all resources. Safe to call even if AIC_Initialize() failed. */
AIIMECORE_API void AIC_Shutdown();

/** UTF-8 description of the last error. Never returns null. */
AIIMECORE_API const char* AIC_GetLastError();

#define AIC_MAX_CANDIDATES 16

#ifdef __cplusplus
}
#endif
