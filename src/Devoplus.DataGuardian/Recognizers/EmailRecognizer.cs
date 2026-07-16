using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Devoplus.DataGuardian.Recognizers;

/// <summary>Detects e-mail addresses.</summary>
/// <remarks>
/// The domain part is written as repeated <c>label "."</c> groups instead of a single greedy class that
/// also matches the dot. This removes the catastrophic-backtracking (ReDoS) behaviour the naive pattern
/// exhibited on long inputs without a TLD dot. A match timeout is also applied as a safety net.
/// </remarks>
public sealed class EmailRecognizer : IPiiRecognizer
{
    static readonly Regex Rx = new(
        @"[A-Z0-9._%+\-]+@(?:[A-Z0-9\-]+\.)+[A-Z]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));

    public IReadOnlyList<PiiHit> Analyze(string text, string lang)
        => Rx.Matches(text).Select(m => new PiiHit(PiiTypes.Email, m.Index, m.Length)).ToList();
}
