using System;
using System.Collections.Generic;

namespace Devoplus.DataGuardian.Recognizers;

public sealed class AddressRecognizer : IPiiRecognizer
{
    static readonly string[] TrKeys = { "mah.", "mahalle", "cad.", "caddesi", "sok.", "sokak", "bulvar", "no:", "daire", "kat" };
    static readonly string[] EnKeys = { "street", "st.", "avenue", "ave", "road", "rd", "no.", "apartment", "zip", "suite" };

    public IReadOnlyList<PiiHit> Analyze(string text, string lang)
    {
        var hits = new List<PiiHit>();
        var low = text.ToLowerInvariant();
        var keys = (lang == "tr") ? TrKeys : EnKeys;
        foreach (var k in keys)
        {
            int idx = 0;
            while ((idx = low.IndexOf(k, idx, StringComparison.Ordinal)) >= 0)
            {
                hits.Add(new PiiHit("ADDRESS", idx, k.Length));
                idx += k.Length;
            }
        }
        return hits;
    }
}