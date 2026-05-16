namespace AiImeShell;

/// <summary>
/// Stub bridge to AiImeCore BERT inference engine.
///
/// Native DLL loading (onnxruntime.dll, AiImeCore.dll) has been moved to an
/// out-of-process inference server (AiImeInference.exe, Phase 5) because
/// loading onnxruntime.dll inside host processes (Word, Notepad, conhost) via
/// TSF injection causes STATUS_HEAP_CORRUPTION (0xc0000374) in ntdll.dll.
///
/// Until Phase 5 is implemented, IsInitialized stays false and Rerank() falls
/// through to dictionary order.  Dictionary lookup is handled by SkkDictionary.
/// </summary>
internal static class AiImeCoreBridge
{
    /// <summary>False until the out-of-process inference server is connected.</summary>
    public static volatile bool IsInitialized = false;

    public static bool Initialize(string modelDir)
    {
        // No-op until out-of-process server is implemented.
        return false;
    }

    public static void Shutdown()
    {
        IsInitialized = false;
    }

    public static string GetLastError() => "Out-of-process inference not yet implemented.";

    /// <summary>
    /// Returns candidates in dictionary order (no BERT reranking).
    /// Called only when IsInitialized is true; never reached in current stub.
    /// </summary>
    public static string[] Rerank(string context, string hiragana, string[] candidates)
        => candidates;
}
