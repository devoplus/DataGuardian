using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Devoplus.DataGuardian.Recognizers;

public sealed class TcknRecognizer : IPiiRecognizer
{
    static readonly Regex Rx = new(@"\b[1-9]\d{10}\b", RegexOptions.Compiled);

    public IReadOnlyList<PiiHit> Analyze(string text, string lang)
    {
        if (lang != "tr") return Array.Empty<PiiHit>();
        var list = new List<PiiHit>();
        foreach (Match m in Rx.Matches(text))
        {
            var v = m.Value;
            if (IsValid(v))
                list.Add(new PiiHit("TCKN", m.Index, m.Length));
        }
        return list;
    }

    static bool IsValid(string s)
    {
        var d = s.Select(c => c - '0').ToArray();
        int odd = d[0] + d[2] + d[4] + d[6] + d[8];
        int even = d[1] + d[3] + d[5] + d[7];
        int d10 = ((odd * 7) - even) % 10;
        if (d10 < 0) d10 += 10;
        if (d[9] != d10) return false;
        int d11 = (d.Take(10).Sum()) % 10;
        return d[10] == d11;
    }
}