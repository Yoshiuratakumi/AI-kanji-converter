using System.Runtime.InteropServices;

namespace AiImeShell;

/// <summary>P/Invoke bridge to the AiImeCore.dll C API.</summary>
internal static class AiImeCoreBridge
{
    private const string DllName = "AiImeCore.dll";

    static AiImeCoreBridge()
    {
        // Assembly.Location is reliable in COM-hosted contexts; AppContext.BaseDirectory is not.
        string dir = Path.GetDirectoryName(
            typeof(AiImeCoreBridge).Assembly.Location) ?? AppContext.BaseDirectory;

        AiImeTextService.Log($"AiImeCoreBridge init, dir={dir}");

        // Preload in dependency order by full path.
        // Preloading libwinpthread before AiImeCore puts it in the process module
        // list, so Windows finds it when resolving AiImeCore's import table.
        // SetDllDirectory is intentionally NOT used — it causes heap corruption in
        // Windows Store / AppContainer processes (e.g. modern Notepad).
        var pthread = Path.Combine(dir, "libwinpthread-1.dll");
        if (File.Exists(pthread)) NativeLibrary.Load(pthread);

        NativeLibrary.Load(Path.Combine(dir, "onnxruntime.dll"));
        NativeLibrary.Load(Path.Combine(dir, "AiImeCore.dll"));
    }

    [DllImport(DllName, EntryPoint = "AIC_Initialize", CharSet = CharSet.Unicode)]
    private static extern int AIC_Initialize_Native(string modelDir);

    [DllImport(DllName, EntryPoint = "AIC_Rerank", CharSet = CharSet.Unicode)]
    private static extern unsafe int AIC_Rerank_Native(
        string context,
        string hiragana,
        char** candidates,
        int candidateCount,
        int* outRankedIndices);

    [DllImport(DllName, EntryPoint = "AIC_LookupDictionary", CharSet = CharSet.Unicode)]
    private static extern unsafe int AIC_LookupDictionary_Native(
        string hiragana,
        char** outCandidates);

    [DllImport(DllName, EntryPoint = "AIC_Shutdown")]
    private static extern void AIC_Shutdown_Native();

    [DllImport(DllName, EntryPoint = "AIC_GetLastError")]
    [return: MarshalAs(UnmanagedType.LPStr)]
    private static extern string AIC_GetLastError_Native();

    // ── Managed wrappers ──────────────────────────────────────────────────────

    /// <summary>True once AIC_Initialize has returned successfully.</summary>
    public static volatile bool IsInitialized = false;

    public static bool Initialize(string modelDir)
    {
        IsInitialized = false;
        bool ok = AIC_Initialize_Native(modelDir) == 0;
        IsInitialized = ok;
        return ok;
    }

    public static void Shutdown()
    {
        IsInitialized = false;
        AIC_Shutdown_Native();
    }

    public static string GetLastError() => AIC_GetLastError_Native() ?? "";

    public static string[] LookupDictionary(string hiragana)
    {
        const int MaxCandidates = 16;
        unsafe
        {
            char** ptrs = stackalloc char*[MaxCandidates];
            int count = AIC_LookupDictionary_Native(hiragana, ptrs);
            if (count <= 0) return [];

            var result = new string[count];
            for (int i = 0; i < count; i++)
                result[i] = new string(ptrs[i]);
            return result;
        }
    }

    public static string[] Rerank(string context, string hiragana, string[] candidates)
    {
        if (candidates.Length == 0) return candidates;
        unsafe
        {
            // Pin all strings and build pointer array
            var handles = new GCHandle[candidates.Length];
            char** ptrs = stackalloc char*[candidates.Length];
            try
            {
                for (int i = 0; i < candidates.Length; i++)
                {
                    handles[i] = GCHandle.Alloc(candidates[i], GCHandleType.Pinned);
                    ptrs[i] = (char*)handles[i].AddrOfPinnedObject();
                }
                int* ranked = stackalloc int[candidates.Length];
                int hr = AIC_Rerank_Native(context, hiragana, ptrs,
                                           candidates.Length, ranked);
                if (hr != 0) return candidates;

                var result = new string[candidates.Length];
                for (int i = 0; i < candidates.Length; i++)
                    result[i] = candidates[ranked[i]];
                return result;
            }
            finally
            {
                foreach (var h in handles)
                    if (h.IsAllocated) h.Free();
            }
        }
    }
}
