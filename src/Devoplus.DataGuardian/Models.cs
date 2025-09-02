using System.Collections.Generic;

namespace Devoplus.DataGuardian;

public sealed record PiiHit(string Type, int Start, int Length);

public interface IPiiRecognizer
{
    IReadOnlyList<PiiHit> Analyze(string text, string lang);
}
