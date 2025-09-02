using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Devoplus.DataGuardian.Ner;

public sealed class BertNerOnnx : INerRecognizer, IDisposable
{
    private readonly InferenceSession? _session;
    private readonly MicrosoftMlTokenizerAdapter? _tok;
    private readonly string[] _labels = Array.Empty<string>();
    private readonly DataGuardianOptions _opt;

    public BertNerOnnx(DataGuardianOptions opt)
    {
        _opt = opt;
        if (!File.Exists(opt.NerModelPath) || !File.Exists(opt.NerTokenizerPath) || !File.Exists(opt.NerLabelsPath))
            return;

        var so = new SessionOptions();
        _session = new InferenceSession(opt.NerModelPath, so);
        _tok = new MicrosoftMlTokenizerAdapter(opt.NerTokenizerPath);
        _labels = File.ReadAllLines(opt.NerLabelsPath);
    }

    public IReadOnlyList<NerEntity> Recognize(string text, string lang)
    {
        if (_session is null || _tok is null || _labels.Length == 0) return Array.Empty<NerEntity>();

        var (ids, mask, typeIds, tokens) = _tok.Encode(text, _opt.NerMaxSequenceLength);
        var shape = new int[] { 1, ids.Length };

        var inputIds = new DenseTensor<long>(shape);
        var attention = new DenseTensor<long>(shape);
        var tokenTypes = new DenseTensor<long>(shape);

        for (int i = 0; i < ids.Length; i++)
        {
            inputIds[0, i] = ids[i];
            attention[0, i] = mask[i];
            tokenTypes[0, i] = typeIds[i];
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attention),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypes)
        };

        using var results = _session.Run(inputs);
        var logits = results.First().AsEnumerable<float>().ToArray();
        int seq = ids.Length;
        int numLabels = _labels.Length;
        if (logits.Length != seq * numLabels) return Array.Empty<NerEntity>();

        var entities = new List<NerEntity>();
        int idx = 0;
        for (int t = 0; t < seq; t++)
        {
            float maxLogit = float.NegativeInfinity;
            int maxK = 0;
            float sumExp = 0;
            var exps = new float[numLabels];
            for (int k = 0; k < numLabels; k++)
            {
                var logit = logits[idx++];
                exps[k] = (float)Math.Exp(logit);
                sumExp += exps[k];
                if (logit > maxLogit) { maxLogit = logit; maxK = k; }
            }
            var prob = exps[maxK] / Math.Max(sumExp, 1e-6f);
            var label = _labels[maxK];
            if (label != "O")
            {
                entities.Add(new NerEntity(TypeFromLabel(label), t, t + 1, prob));
            }
        }
        return MergeEntities(entities);
    }

    private static string TypeFromLabel(string label)
    {
        var baseLabel = label.Contains('-') ? label.Split('-')[1] : label;
        return baseLabel switch
        {
            "PER" => "PERSON",
            "ADDR" => "ADDRESS",
            "EMAIL" => "EMAIL",
            "PHONE" => "PHONE",
            "DATE" => "DOB",
            _ => baseLabel
        };
    }

    private static List<NerEntity> MergeEntities(List<NerEntity> tokens)
    {
        if (tokens.Count == 0) return tokens;
        var merged = new List<NerEntity>();
        var cur = tokens[0];
        for (int i = 1; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (t.Type == cur.Type && t.Start == cur.End)
            {
                cur = cur with { End = t.End, Confidence = Math.Max(cur.Confidence, t.Confidence) };
            }
            else
            {
                merged.Add(cur);
                cur = t;
            }
        }
        merged.Add(cur);
        return merged;
    }

    public void Dispose() => _session?.Dispose();
}