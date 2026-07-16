using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Devoplus.DataGuardian.Recognizers;

/// <summary>Detects payment card numbers (Luhn valid, known scheme prefix).</summary>
/// <remarks>
/// The pattern binds each separator to a following digit so grouped numbers are not truncated at the
/// first space. Scheme detection includes Troy (the Turkish national scheme), Discover, JCB, Maestro
/// and Diners in addition to Visa/Mastercard/Amex.
/// </remarks>
public sealed class CreditCardRecognizer : IPiiRecognizer
{
    static readonly Regex Rx = new(
        @"\b\d(?:[ \-]?\d){12,18}\b",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));

    public IReadOnlyList<PiiHit> Analyze(string text, string lang)
    {
        var list = new List<PiiHit>();
        foreach (Match m in Rx.Matches(text))
        {
            var digits = new string(m.Value.Where(char.IsDigit).ToArray());
            if (digits.Length < 13 || digits.Length > 19) continue;
            if (IsLuhnValid(digits) && IsKnownCardType(digits))
                list.Add(new PiiHit(PiiTypes.CreditCard, m.Index, m.Length));
        }
        return list;
    }

    static bool IsLuhnValid(string s)
    {
        int sum = 0; bool alt = false;
        for (int i = s.Length - 1; i >= 0; i--)
        {
            int n = s[i] - '0';
            if (alt) { n *= 2; if (n > 9) n -= 9; }
            sum += n; alt = !alt;
        }
        return sum % 10 == 0;
    }

    static bool IsKnownCardType(string d)
    {
        int len = d.Length;
        int p2 = int.Parse(d.Substring(0, 2));
        int p3 = int.Parse(d.Substring(0, 3));
        int p4 = int.Parse(d.Substring(0, 4));

        // Visa: 13/16/19 digits, starts with 4
        if ((len == 13 || len == 16 || len == 19) && d[0] == '4') return true;
        // Mastercard: 16 digits, 51-55 or 2221-2720
        if (len == 16 && ((p2 >= 51 && p2 <= 55) || (p4 >= 2221 && p4 <= 2720))) return true;
        // Amex: 15 digits, 34/37
        if (len == 15 && (p2 == 34 || p2 == 37)) return true;
        // Troy (Türkiye): 16 digits, prefix 9792
        if (len == 16 && p4 == 9792) return true;
        // Discover: 16 digits, 6011 / 65 / 644-649
        if (len == 16 && (p4 == 6011 || p2 == 65 || (p3 >= 644 && p3 <= 649))) return true;
        // JCB: 16 digits, 3528-3589
        if (len == 16 && p4 >= 3528 && p4 <= 3589) return true;
        // Maestro: 12-19 digits, 50 / 56-69
        if (len >= 12 && len <= 19 && (p2 == 50 || (p2 >= 56 && p2 <= 69))) return true;
        // Diners Club: 14/16 digits, 36 / 38 / 300-305 / 309
        if ((len == 14 || len == 16) && (p2 == 36 || p2 == 38 || (p3 >= 300 && p3 <= 305) || p3 == 309)) return true;

        return false;
    }
}
