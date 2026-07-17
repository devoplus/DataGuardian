using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Devoplus.DataGuardian.Recognizers;

/// <summary>Detects IBANs of any registered country and validates them with the mod-97 checksum.</summary>
/// <remarks>
/// The pattern tolerates the optional single spaces banks use to group an IBAN in blocks of four
/// (e.g. <c>TR33 0006 1005 1978 6457 8413 26</c>); the value is normalised (spaces removed) before the
/// country-length and mod-97 checks. The hit offset/length always refer to the raw matched text so
/// redaction masks the on-screen characters.
/// </remarks>
public sealed class IbanRecognizer : IPiiRecognizer
{
    // Matches either the compact form (no spaces) or the grouped form banks print
    // (blocks of four separated by single spaces). The grouped branch requires fixed
    // 4-character blocks so it cannot bridge a trailing word after the IBAN.
    static readonly Regex Rx = new(
        @"\b[A-Z]{2}\d{2}(?:[A-Z0-9]{11,30}|(?: [A-Z0-9]{4}){2,7}(?: [A-Z0-9]{1,4})?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));

    // Length of the full IBAN per ISO 13616 / SWIFT IBAN Registry.
    static readonly Dictionary<string, int> IbanLengths = new()
    {
        { "AL", 28 }, { "AD", 24 }, { "AT", 20 }, { "AZ", 28 }, { "BH", 22 }, { "BE", 16 },
        { "BA", 20 }, { "BR", 29 }, { "BG", 22 }, { "BY", 28 }, { "CR", 22 }, { "HR", 21 },
        { "CY", 28 }, { "CZ", 24 }, { "DK", 18 }, { "DO", 28 }, { "EG", 29 }, { "EE", 20 },
        { "FO", 18 }, { "FI", 18 }, { "FR", 27 }, { "GE", 22 }, { "DE", 22 }, { "GI", 23 },
        { "GR", 27 }, { "GL", 18 }, { "GT", 28 }, { "HU", 28 }, { "IS", 26 }, { "IE", 22 },
        { "IL", 23 }, { "IQ", 23 }, { "IT", 27 }, { "JO", 30 }, { "KZ", 20 }, { "XK", 20 },
        { "KW", 30 }, { "LV", 21 }, { "LB", 28 }, { "LI", 21 }, { "LT", 20 }, { "LU", 20 },
        { "LC", 32 }, { "MK", 19 }, { "MT", 31 }, { "MR", 27 }, { "MU", 30 }, { "MC", 27 },
        { "MD", 24 }, { "ME", 22 }, { "NL", 18 }, { "NO", 15 }, { "PK", 24 }, { "PS", 29 },
        { "PL", 28 }, { "PT", 25 }, { "QA", 29 }, { "RO", 24 }, { "SM", 27 }, { "ST", 25 },
        { "SA", 24 }, { "RS", 22 }, { "SC", 31 }, { "SK", 24 }, { "SI", 19 }, { "ES", 24 },
        { "SE", 24 }, { "CH", 21 }, { "SV", 28 }, { "TL", 23 }, { "TN", 24 }, { "TR", 26 },
        { "UA", 29 }, { "AE", 23 }, { "GB", 22 }, { "VA", 22 }, { "VG", 24 }
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
                    list.Add(new PiiHit(PiiTypes.Iban, m.Index, m.Length));
            }
        }
        return list;
    }

    // IBAN mod-97 validation (ISO 7064).
    static bool IsIbanValid(string iban)
    {
        string rearranged = iban.Substring(4) + iban.Substring(0, 4);
        int remainder = 0;
        foreach (char c in rearranged)
        {
            int value = char.IsLetter(c) ? (c - 'A' + 10) : (c - '0');
            if (value < 0 || value > 35) return false;
            remainder = value > 9
                ? (remainder * 100 + value) % 97
                : (remainder * 10 + value) % 97;
        }
        return remainder == 1;
    }
}
