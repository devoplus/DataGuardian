using System;
using System.Collections.Generic;
using System.Linq;

namespace Devoplus.DataGuardian;

/// <summary>
/// Runs the configured recognizers (and optional NER) over a piece of text and produces a 0–10 risk score.
/// </summary>
public sealed class DataGuardianEngine
{
    private readonly DataGuardianOptions _opt;
    private readonly List<IPiiRecognizer> _recognizers;
    private readonly Ner.INerRecognizer? _ner;

    public DataGuardianEngine(DataGuardianOptions opt, Ner.INerRecognizer? ner = null)
    {
        _opt = opt;
        _recognizers = new()
        {
            new Recognizers.EmailRecognizer(),
            new Recognizers.PhoneRecognizer(opt.DefaultPhoneRegion),
            new Recognizers.IbanRecognizer(),
            new Recognizers.CreditCardRecognizer(),
            new Recognizers.TcknRecognizer(),
            new Recognizers.DobRecognizer(),
            new Recognizers.AddressRecognizer()
        };
        _ner = ner;
    }

    public (double risk, Dictionary<string, int> counts) Analyze(string text)
    {
        var result = AnalyzeDetailed(text);
        return (result.risk, result.counts);
    }

    public (double risk, Dictionary<string, int> counts, List<PiiHit> hits) AnalyzeDetailed(string text)
    {
        var hits = new List<PiiHit>();
        if (!string.IsNullOrWhiteSpace(text))
        {
            var lang = _opt.LanguageOverride ?? SimpleLanguage.Guess(text);

            // Pattern recognizers, with overlap resolution so the same span is not counted twice
            // (e.g. a phone-shaped e-mail local part matched by both EMAIL and PHONE).
            var patternHits = _recognizers.SelectMany(r => r.Analyze(text, lang)).ToList();
            hits.AddRange(ResolveOverlaps(patternHits));

            // Optional NER. Its offsets are token based today, so it does not participate in the
            // (character-offset) overlap resolution above.
            if (_opt.EnableNer && _ner is not null)
            {
                foreach (var e in _ner.Recognize(text, lang).Where(e => e.Confidence >= _opt.MinNerConfidence))
                    hits.Add(new PiiHit(e.Type, e.Start, e.End - e.Start));
            }

            // Entity type filters apply to ALL hits (pattern + NER).
            if (_opt.IncludeEntityTypes.Count > 0)
                hits = hits.Where(h => _opt.IncludeEntityTypes.Contains(h.Type)).ToList();
            if (_opt.ExcludeEntityTypes.Count > 0)
                hits = hits.Where(h => !_opt.ExcludeEntityTypes.Contains(h.Type)).ToList();
        }

        var groups = hits.GroupBy(h => h.Type).ToDictionary(g => g.Key, g => g.Count());
        double sum = 0;
        foreach (var (type, cnt) in groups)
            sum += Math.Min(cnt, _opt.MaxCountPerType) * WeightOf(type);

        var risk = 10 * (1 - Math.Exp(-_opt.K * sum));
        risk = Math.Clamp(risk, 0, 10);
        return (Math.Round(risk, 2), groups, hits);
    }

    private double WeightOf(string type)
    {
        // Missing key -> default 1. An explicit 0 disables the type; negatives are treated as 0.
        if (!_opt.Weights.TryGetValue(type, out var w)) return 1;
        return w < 0 ? 0 : w;
    }

    private List<PiiHit> ResolveOverlaps(List<PiiHit> hits)
    {
        if (hits.Count <= 1) return hits;

        var ordered = hits.OrderBy(h => h.Start).ThenByDescending(h => h.Length).ToList();
        var result = new List<PiiHit>();
        foreach (var h in ordered)
        {
            int overlapIdx = -1;
            for (int i = 0; i < result.Count; i++)
            {
                var e = result[i];
                if (h.Start < e.Start + e.Length && e.Start < h.Start + h.Length)
                {
                    overlapIdx = i;
                    break;
                }
            }

            if (overlapIdx < 0)
            {
                result.Add(h);
                continue;
            }

            // Keep the higher-weighted type; tie-break on the longer span.
            var existing = result[overlapIdx];
            if (WeightOf(h.Type) > WeightOf(existing.Type) ||
                (WeightOf(h.Type) == WeightOf(existing.Type) && h.Length > existing.Length))
            {
                result[overlapIdx] = h;
            }
        }
        return result;
    }
}
