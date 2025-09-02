using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Devoplus.DataGuardian.Recognizers;

public sealed class CreditCardRecognizer : IPiiRecognizer
{
    static readonly Regex Rx = new(@"\b(?:\d[ \-]*?){13,19}\b", RegexOptions.Compiled);

    public IReadOnlyList<PiiHit> Analyze(string text, string lang)
    {
        var list = new List<PiiHit>();
        foreach (Match m in Rx.Matches(text))
        {
            var digits = new string(m.Value.Where(char.IsDigit).ToArray());
            if (digits.Length < 13 || digits.Length > 19) continue;
            if (IsLuhnValid(digits) && IsKnownCardType(digits))
                list.Add(new PiiHit("CREDIT_CARD", m.Index, m.Length));
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

    static bool IsKnownCardType(string digits)
    {
        // Visa: 16 hane, 4 ile başlar
        if (digits.Length == 16 && digits.StartsWith("4"))
            return true;
        // MasterCard: 16 hane, 51-55 veya 2221-2720 ile başlar
        if (digits.Length == 16)
        {
            int prefix2 = int.Parse(digits.Substring(0, 2));
            int prefix4 = int.Parse(digits.Substring(0, 4));
            if ((prefix2 >= 51 && prefix2 <= 55) || (prefix4 >= 2221 && prefix4 <= 2720))
                return true;
        }
        // Amex: 15 hane, 34 veya 37 ile başlar
        if (digits.Length == 15 && (digits.StartsWith("34") || digits.StartsWith("37")))
            return true;
        return false;
    }
}