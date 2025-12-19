using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Devoplus.DataGuardian.Recognizers;

public sealed class VknRecognizer : IPiiRecognizer
{
    static readonly Regex Rx = new(@"\b\d{10}\b", RegexOptions.Compiled);

    public IReadOnlyList<PiiHit> Analyze(string text, string lang)
    {
        var list = new List<PiiHit>();
        foreach (Match m in Rx.Matches(text))
        {
            var v = m.Value;
            if (IsValid(v))
                list.Add(new PiiHit("VKN", m.Index, m.Length));
        }
        return list;
    }

    static bool IsValid(string s)
    {
        if (s.Length != 10) return false;
        
        // VKN checksum validation (Modulo 10 algorithm)
        var digits = s.Select(c => c - '0').ToArray();
        
        int[] v = new int[10];
        for (int i = 0; i < 9; i++)
        {
            int temp = (digits[i] + (9 - i)) % 10;
            v[i] = (temp * (int)Math.Pow(2, 9 - i)) % 9;
            if (temp != 0 && v[i] == 0) v[i] = 9;
        }
        
        int sum = v.Take(9).Sum();
        int lastDigit = (10 - (sum % 10)) % 10;
        
        return digits[9] == lastDigit;
    }
}
