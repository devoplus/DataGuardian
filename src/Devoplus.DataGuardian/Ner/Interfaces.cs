using System.Collections.Generic;

namespace Devoplus.DataGuardian.Ner;

public sealed record NerEntity(string Type, int Start, int End, double Confidence);

public interface INerRecognizer
{
    IReadOnlyList<NerEntity> Recognize(string text, string lang);
}
