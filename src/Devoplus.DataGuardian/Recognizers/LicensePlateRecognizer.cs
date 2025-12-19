using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Devoplus.DataGuardian.Recognizers;

public sealed class LicensePlateRecognizer : IPiiRecognizer
{
    // Turkish license plate format: 2 digits + space + 1-3 letters + space + 2-4 digits
    // Examples: "34 ABC 1234", "06 XY 9876", "01 A 1234"
    static readonly Regex Rx = new(@"\b\d{2}\s?[A-Z]{1,3}\s?\d{2,4}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public IReadOnlyList<PiiHit> Analyze(string text, string lang)
    {
        if (lang != "tr") return System.Array.Empty<PiiHit>();
        
        var list = new List<PiiHit>();
        foreach (Match m in Rx.Matches(text))
        {
            list.Add(new PiiHit("LICENSE_PLATE", m.Index, m.Length));
        }
        return list;
    }
}
