#include "../include/AiImeCore.h"

#include <cstdio>
#include <cstring>
#include <string>
#include <vector>
#include <windows.h>

// ── Minimal test harness ──────────────────────────────────────────────────────

static int g_passed = 0;
static int g_failed = 0;

#define EXPECT_TRUE(expr)                                                  \
    do {                                                                   \
        if (expr) {                                                        \
            ++g_passed;                                                    \
        } else {                                                           \
            ++g_failed;                                                    \
            fprintf(stderr, "FAIL  %s:%d  %s\n", __FILE__, __LINE__, #expr); \
        }                                                                  \
    } while (0)

#define EXPECT_EQ(a, b)  EXPECT_TRUE((a) == (b))
#define EXPECT_GE(a, b)  EXPECT_TRUE((a) >= (b))
#define EXPECT_LE(a, b)  EXPECT_TRUE((a) <= (b))
#define EXPECT_NE(a, b)  EXPECT_TRUE((a) != (b))

// ── Helpers ───────────────────────────────────────────────────────────────────

static std::wstring ToWide(const char* s)
{
    int n = MultiByteToWideChar(CP_UTF8, 0, s, -1, nullptr, 0);
    std::wstring w(n - 1, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, s, -1, w.data(), n);
    return w;
}

// ── Tests ─────────────────────────────────────────────────────────────────────

static void TestDictionaryLookup()
{
    printf("--- TestDictionaryLookup ---\n");

    const wchar_t* ptrs[AIC_MAX_CANDIDATES] = {};

    // "かいぎ" should return candidates like 会議, 開議, etc.
    int n = AIC_LookupDictionary(L"かいぎ", ptrs);  // かいぎ
    EXPECT_GE(n, 1);
    if (n > 0)
        printf("  かいぎ → %d candidate(s): %ls ...\n", n, ptrs[0]);

    // "てんき" should return 天気, etc.
    n = AIC_LookupDictionary(L"てんき", ptrs);  // てんき
    EXPECT_GE(n, 1);
    if (n > 0)
        printf("  てんき → %d candidate(s): %ls ...\n", n, ptrs[0]);

    // Unknown hiragana should return 0
    n = AIC_LookupDictionary(L"ああああああああああ", ptrs);  // aaaaaaaaaa
    EXPECT_EQ(n, 0);
}

static void TestRerankOrdering(const std::wstring& modelDir)
{
    printf("--- TestRerankOrdering ---\n");

    // Context: a sentence about weather → 天気 should outscore 転機 / 電気
    std::wstring context = ToWide(
        "今日は良い。明日の"
        // 今日は良い。明日の
    );

    // Candidates for "てんき"
    const wchar_t* candidates[] = {
        L"天気",   // 天気
        L"転機",   // 転機
        L"電気",   // 電気
    };
    int n = 3;
    int ranked[3] = {};
    int ret = AIC_Rerank(context.c_str(), L"てんき", candidates, n, ranked);

    EXPECT_EQ(ret, 3);
    printf("  Ranked: ");
    for (int i = 0; i < ret; ++i)
        printf("%ls ", candidates[ranked[i]]);
    printf("\n");

    // 天気 (idx 0) should be #1 in a weather context
    EXPECT_EQ(ranked[0], 0);
}

static void TestRerankWithEmptyContext(const std::wstring& /*modelDir*/)
{
    printf("--- TestRerankWithEmptyContext ---\n");

    const wchar_t* candidates[] = {L"会議", L"開議"};  // 会議, 開議
    int ranked[2] = {};
    int ret = AIC_Rerank(L"", L"かいぎ", candidates, 2, ranked);

    EXPECT_EQ(ret, 2);
    printf("  Ranked with empty context: %ls %ls\n",
           candidates[ranked[0]], candidates[ranked[1]]);
}

static void TestErrorHandling()
{
    printf("--- TestErrorHandling ---\n");

    // Null hiragana
    const wchar_t* ptrs[AIC_MAX_CANDIDATES] = {};
    int n = AIC_LookupDictionary(nullptr, ptrs);
    EXPECT_EQ(n, -1);

    // Zero candidates
    int ranked[1] = {};
    int ret = AIC_Rerank(L"", L"あ", nullptr, 0, ranked);
    EXPECT_EQ(ret, -1);

    const char* err = AIC_GetLastError();
    EXPECT_NE(err, nullptr);
    printf("  Last error: %s\n", err);
}

// ── Entry point ───────────────────────────────────────────────────────────────

int main(int argc, char* argv[])
{
    if (argc < 2) {
        fprintf(stderr, "Usage: test_core <modelDir>\n");
        fprintf(stderr, "  modelDir must contain bert_mlm.onnx, vocab.txt, dict.skk\n");
        return 1;
    }

    std::wstring modelDir = ToWide(argv[1]);

    printf("=== AiImeCore unit tests ===\n");
    printf("Model dir: %ls\n\n", modelDir.c_str());

    // Initialize
    int init = AIC_Initialize(modelDir.c_str());
    if (init != 0) {
        fprintf(stderr, "AIC_Initialize failed (%d): %s\n", init, AIC_GetLastError());
        fprintf(stderr, "Make sure bert_mlm.onnx and vocab.txt are present.\n");
        return 2;
    }
    printf("AIC_Initialize OK\n\n");

    TestDictionaryLookup();
    printf("\n");

    TestRerankOrdering(modelDir);
    printf("\n");

    TestRerankWithEmptyContext(modelDir);
    printf("\n");

    TestErrorHandling();
    printf("\n");

    AIC_Shutdown();

    printf("=== Results: %d passed, %d failed ===\n", g_passed, g_failed);
    return (g_failed == 0) ? 0 : 1;
}
