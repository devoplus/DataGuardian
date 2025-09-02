using PhoneNumbers;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Devoplus.DataGuardian.Recognizers;

public sealed class PhoneRecognizer : IPiiRecognizer
{
    public IReadOnlyList<PiiHit> Analyze(string text, string lang)
    {
        var list = new List<PiiHit>();
        var phoneUtil = PhoneNumberUtil.GetInstance();

        var rx = new Regex(@"\+?\d[\d\s\-()]{7,}", RegexOptions.Compiled);
        foreach (Match m in rx.Matches(text))
        {
            var candidate = m.Value;
            try
            {
                PhoneNumber number;
                if (candidate.TrimStart().StartsWith("+"))
                {
                    number = phoneUtil.Parse(candidate, null);
                }
                else
                {
                    number = phoneUtil.Parse(candidate, "ZZ");
                }

                if (phoneUtil.IsValidNumber(number))
                {
                    list.Add(new PiiHit("PHONE", m.Index, m.Length));
                }
            }
            catch (NumberParseException)
            {
                // Geçersiz ise atla
            }
        }
        return list;
    }
}