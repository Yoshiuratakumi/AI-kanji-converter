using System.Text;

namespace AiImeShell;

/// <summary>
/// Converts romaji key sequences to hiragana incrementally.
/// Handles standard roomaji rules including っ (double consonant) and ん.
/// </summary>
internal sealed class RomajiConverter
{
    private string _pending = "";

    private static readonly Dictionary<string, string> _table = BuildTable();

    public string Pending => _pending;

    /// <summary>
    /// Feed a character. Returns the hiragana produced (may be empty if more
    /// input is needed to complete a mora), or null if the character is not
    /// part of romaji input.
    /// </summary>
    public string? Feed(char c)
    {
        char lower = char.ToLowerInvariant(c);
        if (lower < 'a' || lower > 'z') return null;

        string candidate = _pending + lower;

        // ん: n followed by non-na/ni/nu/ne/no/nya/nyi/nyu/nye/nyo and non-n
        if (_pending == "n" && lower != 'a' && lower != 'i' && lower != 'u' &&
            lower != 'e' && lower != 'o' && lower != 'y' && lower != 'n')
        {
            _pending = "" + lower;
            return "ん";
        }

        // Double consonant → っ
        if (_pending.Length == 1 && _pending[0] == lower && lower != 'n')
        {
            _pending = "" + lower;
            return "っ";
        }

        // Try exact match
        if (_table.TryGetValue(candidate, out string? exact))
        {
            _pending = "";
            return exact;
        }

        // If no match is possible from this prefix, flush pending as error and restart
        if (!_table.Keys.Any(k => k.StartsWith(candidate, StringComparison.Ordinal)))
        {
            // Pending could not form a mora — restart with current char
            string flush = _pending;
            _pending = "" + lower;
            // Try to interpret flush alone (shouldn't happen for valid romaji)
            return null;
        }

        _pending = candidate;
        return "";
    }

    /// <summary>Force-flushes pending buffer. Returns ん if pending=="n".</summary>
    public string Flush()
    {
        string p = _pending;
        _pending = "";
        return p == "n" ? "ん" : "";
    }

    public void Clear() => _pending = "";

    // ── Table ─────────────────────────────────────────────────────────────────

    private static Dictionary<string, string> BuildTable()
    {
        var t = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"]   = "あ", ["i"]   = "い", ["u"]   = "う",
            ["e"]   = "え", ["o"]   = "お",
            ["ka"]  = "か", ["ki"]  = "き", ["ku"]  = "く",
            ["ke"]  = "け", ["ko"]  = "こ",
            ["sa"]  = "さ", ["si"]  = "し", ["shi"] = "し",
            ["su"]  = "す", ["se"]  = "せ", ["so"]  = "そ",
            ["ta"]  = "た", ["ti"]  = "ち", ["chi"] = "ち",
            ["tu"]  = "つ", ["tsu"] = "つ",
            ["te"]  = "て", ["to"]  = "と",
            ["na"]  = "な", ["ni"]  = "に", ["nu"]  = "ぬ",
            ["ne"]  = "ね", ["no"]  = "の",
            ["ha"]  = "は", ["hi"]  = "ひ", ["hu"]  = "ふ",
            ["fu"]  = "ふ", ["he"]  = "へ", ["ho"]  = "ほ",
            ["ma"]  = "ま", ["mi"]  = "み", ["mu"]  = "む",
            ["me"]  = "め", ["mo"]  = "も",
            ["ya"]  = "や", ["yu"]  = "ゆ", ["yo"]  = "よ",
            ["ra"]  = "ら", ["ri"]  = "り", ["ru"]  = "る",
            ["re"]  = "れ", ["ro"]  = "ろ",
            ["wa"]  = "わ", ["wi"]  = "ゐ", ["we"]  = "ゑ",
            ["wo"]  = "を", ["nn"]  = "ん",
            ["ga"]  = "が", ["gi"]  = "ぎ", ["gu"]  = "ぐ",
            ["ge"]  = "げ", ["go"]  = "ご",
            ["za"]  = "ざ", ["zi"]  = "じ", ["ji"]  = "じ",
            ["zu"]  = "ず", ["ze"]  = "ぜ", ["zo"]  = "ぞ",
            ["da"]  = "だ", ["di"]  = "ぢ", ["du"]  = "づ",
            ["de"]  = "で", ["do"]  = "ど",
            ["ba"]  = "ば", ["bi"]  = "び", ["bu"]  = "ぶ",
            ["be"]  = "べ", ["bo"]  = "ぼ",
            ["pa"]  = "ぱ", ["pi"]  = "ぴ", ["pu"]  = "ぷ",
            ["pe"]  = "ぺ", ["po"]  = "ぽ",
            // compound morae
            ["kya"] = "きゃ", ["kyu"] = "きゅ", ["kyo"] = "きょ",
            ["sha"] = "しゃ", ["shu"] = "しゅ", ["sho"] = "しょ",
            ["sya"] = "しゃ", ["syu"] = "しゅ", ["syo"] = "しょ",
            ["cha"] = "ちゃ", ["chu"] = "ちゅ", ["cho"] = "ちょ",
            ["tya"] = "ちゃ", ["tyu"] = "ちゅ", ["tyo"] = "ちょ",
            ["nya"] = "にゃ", ["nyu"] = "にゅ", ["nyo"] = "にょ",
            ["hya"] = "ひゃ", ["hyu"] = "ひゅ", ["hyo"] = "ひょ",
            ["mya"] = "みゃ", ["myu"] = "みゅ", ["myo"] = "みょ",
            ["rya"] = "りゃ", ["ryu"] = "りゅ", ["ryo"] = "りょ",
            ["gya"] = "ぎゃ", ["gyu"] = "ぎゅ", ["gyo"] = "ぎょ",
            ["ja"]  = "じゃ", ["ju"]  = "じゅ", ["jo"]  = "じょ",
            ["jya"] = "じゃ", ["jyu"] = "じゅ", ["jyo"] = "じょ",
            ["dya"] = "ぢゃ", ["dyu"] = "ぢゅ", ["dyo"] = "ぢょ",
            ["bya"] = "びゃ", ["byu"] = "びゅ", ["byo"] = "びょ",
            ["pya"] = "ぴゃ", ["pyu"] = "ぴゅ", ["pyo"] = "ぴょ",
            // small kana via x/l prefix
            ["xa"]  = "ぁ", ["xi"]  = "ぃ", ["xu"]  = "ぅ",
            ["xe"]  = "ぇ", ["xo"]  = "ぉ",
            ["xya"] = "ゃ", ["xyu"] = "ゅ", ["xyo"] = "ょ",
            ["xtu"] = "っ", ["ltsu"] = "っ",
        };
        return t;
    }
}
