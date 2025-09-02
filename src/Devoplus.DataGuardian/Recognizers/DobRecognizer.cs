using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Devoplus.DataGuardian.Recognizers;

public sealed class DobRecognizer : IPiiRecognizer
{
    static readonly Regex Rx = new(@"\b(?:(\d{4})[-/.](\d{1,2})[-/.](\d{1,2})|(\d{1,2})[-/.](\d{1,2})[-/.](\d{4}))\b", RegexOptions.Compiled);
    public IReadOnlyList<PiiHit> Analyze(string text, string lang)
        => Rx.Matches(text).Select(m => new PiiHit("DOB", m.Index, m.Length)).ToList();
}