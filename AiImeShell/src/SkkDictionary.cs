using System.Text;

namespace AiImeShell;

/// <summary>
/// Pure-C# SKK dictionary reader.
/// Replaces the P/Invoke path to avoid loading onnxruntime.dll (which causes
/// heap corruption when injected into host processes like Word and Notepad).
/// </summary>
internal static class SkkDictionary
{
    private static readonly Dictionary<string, string[]> _entries =
        new(StringComparer.Ordinal);

    private static int _loadState; // 0=not started, 1=loading, 2=done

    public static void Load(string path)
    {
        // Only load once per process, even if called concurrently from multiple Activate() calls.
        if (Interlocked.CompareExchange(ref _loadState, 1, 0) != 0) return;

        if (!File.Exists(path))
        {
            AiImeTextService.Log($"SkkDictionary: file not found: {path}");
            Interlocked.Exchange(ref _loadState, 2);
            return;
        }

        try
        {
            // The dict.skk header says euc-jp but the actual bytes are UTF-8.
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            bool inNasi = false;

            foreach (var rawLine in lines)
            {
                // Strip CR from CRLF files
                var line = rawLine.TrimEnd('\r');

                if (line.StartsWith(";;"))
                {
                    if (line.Contains("okuri-nasi")) inNasi = true;
                    continue;
                }

                // Only load okuri-nasi (non-inflected) entries.
                // Okuri-ari entries have a trailing ASCII letter in the reading
                // (e.g. "われr /我/") and are handled by the okuri-ari section.
                if (!inNasi) continue;
                if (line.Length == 0) continue;

                int sp = line.IndexOf(' ');
                if (sp <= 0) continue;

                string reading = line[..sp];

                // Skip okuri-ari entries that appear after the nasi marker
                // (reading ends with ASCII letter a-z)
                if (char.IsAsciiLetter(reading[^1])) continue;

                string rest = line[(sp + 1)..];

                // Candidates are slash-delimited: /cand1/cand2;annotation/
                var parts = rest.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var candidates = new List<string>(parts.Length);
                foreach (var part in parts)
                {
                    if (part.StartsWith(';')) continue; // trailing comment
                    // Strip SKK annotation: "漢字;注記" → "漢字"
                    int semi = part.IndexOf(';');
                    string cand = semi >= 0 ? part[..semi] : part;
                    if (cand.Length > 0) candidates.Add(cand);
                }

                if (candidates.Count > 0)
                    _entries[reading] = candidates.ToArray();
            }

            Interlocked.Exchange(ref _loadState, 2);
            AiImeTextService.Log($"SkkDictionary: loaded {_entries.Count} entries");
        }
        catch (Exception ex)
        {
            AiImeTextService.Log($"SkkDictionary load error: {ex.Message}");
            Interlocked.Exchange(ref _loadState, 2);
        }
    }

    public static string[] Lookup(string hiragana, int maxCount = 16)
    {
        if (_loadState < 2 || !_entries.TryGetValue(hiragana, out var all))
            return [];
        return all.Length <= maxCount ? all : all[..maxCount];
    }
}
