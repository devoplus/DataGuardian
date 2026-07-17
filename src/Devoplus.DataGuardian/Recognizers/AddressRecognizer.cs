using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Devoplus.DataGuardian.Recognizers;

/// <summary>Flags likely postal-address text by matching whole-word address keywords.</summary>
/// <remarks>
/// Keywords are matched on word boundaries so common substrings no longer produce false positives
/// (e.g. "have"→"ave", "third"/"word"→"rd", "katalog"→"kat"). The hit spans only the keyword; ADDRESS
/// is therefore intended for tagging/scoring and is excluded from the default redaction set, since
/// masking a keyword would not remove the identifying address text around it.
/// </remarks>
public sealed class AddressRecognizer : IPiiRecognizer
{
    static readonly string[] TrKeys = { "mah.", "mahalle", "cad.", "caddesi", "sok.", "sokak", "bulvar", "no:", "daire", "kat" };
    static readonly string[] EnKeys = { "street", "st.", "avenue", "ave", "road", "rd", "no.", "apartment", "zip", "suite" };

    static readonly Regex TrRx = BuildKeywordRegex(TrKeys);
    static readonly Regex EnRx = BuildKeywordRegex(EnKeys);

    public IReadOnlyList<PiiHit> Analyze(string text, string lang)
    {
        var rx = (lang == "tr") ? TrRx : EnRx;
        var hits = new List<PiiHit>();
        foreach (Match m in rx.Matches(text))
            hits.Add(new PiiHit(PiiTypes.Address, m.Index, m.Length));
        return hits;
    }

    static Regex BuildKeywordRegex(string[] keys)
    {
        var alternatives = keys.Select(raw =>
        {
            var k = raw.Trim();
            var pre = char.IsLetterOrDigit(k[0]) ? @"\b" : "";
            var post = char.IsLetterOrDigit(k[^1]) ? @"\b" : "";
            return pre + Regex.Escape(k) + post;
        });

        return new Regex(
            "(?:" + string.Join("|", alternatives) + ")",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(200));
    }
}
