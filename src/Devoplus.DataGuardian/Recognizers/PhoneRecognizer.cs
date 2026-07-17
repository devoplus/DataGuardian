using PhoneNumbers;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Devoplus.DataGuardian.Recognizers;

/// <summary>Detects telephone numbers using libphonenumber for validation.</summary>
/// <remarks>
/// National-format numbers (without a leading <c>+</c>) require a valid default region to parse.
/// The previous implementation passed <c>"ZZ"</c> (unknown region), which always threw for national
/// numbers, so numbers such as <c>05xx xxx xx xx</c> were never detected. The region is now derived
/// from the detected language and a configurable default (see <see cref="DataGuardianOptions.DefaultPhoneRegion"/>).
/// </remarks>
public sealed class PhoneRecognizer : IPiiRecognizer
{
    static readonly Regex Rx = new(
        @"\+?\d[\d \-()]{6,}\d",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));

    static readonly PhoneNumberUtil PhoneUtil = PhoneNumberUtil.GetInstance();

    readonly string _defaultRegion;

    public PhoneRecognizer(string? defaultRegion = null)
        => _defaultRegion = string.IsNullOrWhiteSpace(defaultRegion) ? "TR" : defaultRegion!.ToUpperInvariant();

    public IReadOnlyList<PiiHit> Analyze(string text, string lang)
    {
        var list = new List<PiiHit>();
        var region = lang == "tr" ? "TR" : _defaultRegion;

        foreach (Match m in Rx.Matches(text))
        {
            var candidate = m.Value;
            try
            {
                var number = candidate.TrimStart().StartsWith('+')
                    ? PhoneUtil.Parse(candidate, null)
                    : PhoneUtil.Parse(candidate, region);

                if (PhoneUtil.IsValidNumber(number))
                    list.Add(new PiiHit(PiiTypes.Phone, m.Index, m.Length));
            }
            catch (NumberParseException)
            {
                // Not a parseable phone number; skip.
            }
        }
        return list;
    }
}
