using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Devoplus.DataGuardian.Recognizers;

public sealed class SgkRecognizer : IPiiRecognizer
{
    static readonly Regex Rx = new(@"\b\d{12}\b", RegexOptions.Compiled);

    public IReadOnlyList<PiiHit> Analyze(string text, string lang)
    {
        var list = new List<PiiHit>();
        foreach (Match m in Rx.Matches(text))
        {
            // SGK numbers are 12 digits
            // Basic validation: should be all digits
            list.Add(new PiiHit("SGK", m.Index, m.Length));
        }
        return list;
    }
}
