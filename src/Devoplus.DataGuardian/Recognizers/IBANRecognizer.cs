using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Devoplus.DataGuardian.Recognizers;

public sealed class IbanRecognizer : IPiiRecognizer
{
    static readonly Regex Rx = new(@"\b([A-Z]{2})(\d{2})([A-Z0-9]{11,30})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    static readonly Dictionary<string, int> IbanLengths = new()
    {
        { "AL", 28 }, { "AD", 24 }, { "AT", 20 }, { "AZ", 28 }, { "BH", 22 }, { "BE", 16 },
        { "BA", 20 }, { "BR", 29 }, { "BG", 22 }, { "CR", 22 }, { "HR", 21 }, { "CY", 28 },
        { "CZ", 24 }, { "DK", 18 }, { "DO", 28 }, { "EE", 20 }, { "FO", 18 }, { "FI", 18 },
        { "FR", 27 }, { "GE", 22 }, { "DE", 22 }, { "GI", 23 }, { "GR", 27 }, { "GL", 18 },
        { "GT", 28 }, { "HU", 28 }, { "IS", 26 }, { "IE", 22 }, { "IL", 23 }, { "IT", 27 },
        { "JO", 30 }, { "KZ", 20 }, { "KW", 30 }, { "LV", 21 }, { "LB", 28 }, { "LI", 21 },
        { "LT", 20 }, { "LU", 20 }, { "MK", 19 }, { "MT", 31 }, { "MR", 27 }, { "MU", 30 },
        { "MC", 27 }, { "MD", 24 }, { "ME", 22 }, { "NL", 18 }, { "NO", 15 }, { "PK", 24 },
        { "PS", 29 }, { "PL", 28 }, { "PT", 25 }, { "QA", 29 }, { "RO", 24 }, { "SM", 27 },
        { "SA", 24 }, { "RS", 22 }, { "SK", 24 }, { "SI", 19 }, { "ES", 24 }, { "SE", 24 },
        { "CH", 21 }, { "TN", 24 }, { "TR", 26 }, { "AE", 23 }, { "GB", 22 }, { "VG", 24 }

        // Liste yapay zeka kullanılarak oluşturuldu. Bazı ülkeler için teyit etmek gerekir.
    };

    public IReadOnlyList<PiiHit> Analyze(string text, string lang)
    {
        var list = new List<PiiHit>();
        foreach (Match m in Rx.Matches(text))
        {
            var iban = m.Value.Replace(" ", "").ToUpperInvariant();
            if (iban.Length < 15 || iban.Length > 34) continue;
            var country = iban.Substring(0, 2);
            if (IbanLengths.TryGetValue(country, out int expectedLen) && iban.Length == expectedLen)
            {
                if (IsIbanValid(iban))
                    list.Add(new PiiHit("IBAN", m.Index, m.Length));
            }
        }
        return list;
    }

    // IBAN Mod-97 doğrulaması
    static bool IsIbanValid(string iban)
    {
        string rearranged = iban.Substring(4) + iban.Substring(0, 4);
        string numeric = string.Concat(rearranged.Select(c => char.IsLetter(c) ? (c - 'A' + 10).ToString() : c.ToString()));
        int remainder = 0;
        foreach (char ch in numeric)
            remainder = (remainder * 10 + (ch - '0')) % 97;
        return remainder == 1;
    }
}