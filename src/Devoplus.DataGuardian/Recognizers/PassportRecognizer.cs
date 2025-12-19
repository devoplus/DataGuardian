using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Devoplus.DataGuardian.Recognizers;

public sealed class PassportRecognizer : IPiiRecognizer
{
    // Turkish passport format: 1 letter + 8 digits (e.g., "U12345678")
    static readonly Regex Rx = new(@"\b[A-Z]\d{8}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public IReadOnlyList<PiiHit> Analyze(string text, string lang)
    {
        // Support both Turkish and English contexts
        if (lang != "tr" && lang != "en") return System.Array.Empty<PiiHit>();
        
        var list = new List<PiiHit>();
        foreach (Match m in Rx.Matches(text))
        {
            list.Add(new PiiHit("PASSPORT", m.Index, m.Length));
        }
        return list;
    }
}
