using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Devoplus.DataGuardian;

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
            new Recognizers.PhoneRecognizer(),
            new Recognizers.IbanRecognizer(),
            new Recognizers.CreditCardRecognizer(),
            new Recognizers.TcknRecognizer(),
            new Recognizers.DobRecognizer(),
            new Recognizers.AddressRecognizer()
        };
        _ner = ner;
    }

    public (double risk, Dictionary<string,int> counts) Analyze(string text)
    {
        var result = AnalyzeDetailed(text);
        return (result.risk, result.counts);
    }

    public (double risk, Dictionary<string,int> counts, List<PiiHit> hits) AnalyzeDetailed(string text)
    {
        var hits = new List<PiiHit>();
        if (!string.IsNullOrWhiteSpace(text))
        {
            var lang = _opt.LanguageOverride ?? SimpleLanguage.Guess(text);

            // Pattern recognizers
            hits = _recognizers.SelectMany(r => r.Analyze(text, lang)).ToList();

            // Entity filters
            if (_opt.IncludeEntityTypes.Count > 0)
                hits = hits.Where(h => _opt.IncludeEntityTypes.Contains(h.Type)).ToList();
            if (_opt.ExcludeEntityTypes.Count > 0)
                hits = hits.Where(h => !_opt.ExcludeEntityTypes.Contains(h.Type)).ToList();

            // Optional NER
            if (_opt.EnableNer && _ner is not null && File.Exists(_opt.NerModelPath))
            {
                var ents = _ner.Recognize(text, lang);
                var filtered = ents.Where(e => e.Confidence >= _opt.MinNerConfidence);
                foreach (var e in filtered)
                    hits.Add(new PiiHit(e.Type, e.Start, e.End - e.Start));
            }
        }

        var groups = hits.GroupBy(h => h.Type).ToDictionary(g => g.Key, g => g.Count());
        double sum = 0;
        foreach (var (type, cnt) in groups)
        {
            _opt.Weights.TryGetValue(type, out var w);
            if (w <= 0) w = 1;
            sum += Math.Min(cnt, _opt.MaxCountPerType) * w;
        }
        var risk = 10 * (1 - Math.Exp(-_opt.K * sum));
        return (Math.Round(risk, 2), groups, hits);
    }
}
