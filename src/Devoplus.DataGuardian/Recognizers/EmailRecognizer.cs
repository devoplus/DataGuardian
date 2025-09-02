using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Devoplus.DataGuardian.Recognizers;

public sealed class EmailRecognizer : IPiiRecognizer
{
    static readonly Regex Rx = new(@"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    public IReadOnlyList<PiiHit> Analyze(string text, string lang)
        => Rx.Matches(text).Select(m => new PiiHit("EMAIL", m.Index, m.Length)).ToList();
}