using System;
using System.Collections.Generic;

namespace Devoplus.DataGuardian;

/// <summary>Configuration for the DataGuardian middleware and engine.</summary>
public sealed class DataGuardianOptions
{
    /// <summary>Content types whose bodies are analyzed. A missing/empty content type is also analyzed.</summary>
    public HashSet<string> AnalyzableContentTypes { get; set; } =
        new(StringComparer.OrdinalIgnoreCase)
        { "application/json", "text/plain", "text/html", "text/csv", "application/xml", "application/x-www-form-urlencoded" };

    /// <summary>Per-type risk weights. Keys must match the recognizer output types (see <see cref="PiiTypes"/>).</summary>
    public Dictionary<string, double> Weights { get; set; } = new()
    {
        [PiiTypes.Tckn] = 10, [PiiTypes.CreditCard] = 9, [PiiTypes.Iban] = 8,
        [PiiTypes.Dob] = 7, [PiiTypes.Address] = 6, [PiiTypes.Phone] = 5,
        [PiiTypes.Email] = 4, [PiiTypes.Person] = 3
    };

    /// <summary>Maximum number of hits of a single type that contribute to the score.</summary>
    public int MaxCountPerType { get; set; } = 5;

    /// <summary>Risk curve steepness in <c>10 * (1 - e^(-K * weightedCount))</c>. Must be &gt; 0.</summary>
    public double K { get; set; } = 0.15;

    /// <summary>Analyze request bodies.</summary>
    public bool AnalyzeRequests { get; set; } = true;

    /// <summary>Analyze response bodies.</summary>
    public bool AnalyzeResponses { get; set; } = true;

    /// <summary>Block threshold (0–10). A negative value disables blocking even in <see cref="ActionMode.Block"/>.</summary>
    public double BlockAt { get; set; } = -1;

    /// <summary>Maximum request/response body size (bytes) that is buffered and analyzed.</summary>
    public int MaxBodySizeBytes { get; set; } = 512 * 1024;

    /// <summary>
    /// When true and a body exceeds <see cref="MaxBodySizeBytes"/> in <see cref="ActionMode.Block"/> mode,
    /// the request is blocked (fail closed) instead of passed through unanalyzed. Off by default.
    /// </summary>
    public bool BlockOversizeBodies { get; set; } = false;

    // NER (optional)
    /// <summary>Enable the optional ONNX NER recognizer.</summary>
    public bool EnableNer { get; set; } = false;
    /// <summary>Path to the ONNX NER model.</summary>
    public string NerModelPath { get; set; } = "models/kvkk-ner.onnx";
    /// <summary>Path to the tokenizer JSON.</summary>
    public string NerTokenizerPath { get; set; } = "models/tokenizer.json";
    /// <summary>Path to the label list.</summary>
    public string NerLabelsPath { get; set; } = "models/labels.txt";
    /// <summary>Maximum token sequence length passed to the model.</summary>
    public int NerMaxSequenceLength { get; set; } = 256;
    /// <summary>Minimum confidence (0–1) for an NER entity to be kept.</summary>
    public double MinNerConfidence { get; set; } = 0.6;

    /// <summary>Prefix for the emitted headers (e.g. <c>X-DataGuardian</c>).</summary>
    public string HeaderPrefix { get; set; } = "X-DataGuardian";

    // Path & method filters
    /// <summary>If non-empty, only paths starting with one of these are processed.</summary>
    public List<string> IncludePaths { get; set; } = new();
    /// <summary>Paths starting with one of these are skipped.</summary>
    public List<string> ExcludePaths { get; set; } = new();
    /// <summary>If non-empty, only these HTTP methods are processed (case-insensitive).</summary>
    public List<string> IncludeMethods { get; set; } = new();
    /// <summary>These HTTP methods are skipped (case-insensitive).</summary>
    public List<string> ExcludeMethods { get; set; } = new();

    // Entity filters
    /// <summary>If non-empty, only these entity types are kept (empty = all).</summary>
    public HashSet<string> IncludeEntityTypes { get; set; } = new();
    /// <summary>These entity types are dropped.</summary>
    public HashSet<string> ExcludeEntityTypes { get; set; } = new();

    // Action mode
    /// <summary>Action to take: <see cref="ActionMode.None"/>, <see cref="ActionMode.Tag"/>, <see cref="ActionMode.Redact"/> or <see cref="ActionMode.Block"/>.</summary>
    public ActionMode Action { get; set; } = ActionMode.Tag;
    /// <summary>Redaction threshold (0–10). Response is redacted when risk &gt;= this value.</summary>
    public double RedactAt { get; set; } = 0;
    /// <summary>Entity types eligible for redaction. Keys must match <see cref="PiiTypes"/>.</summary>
    public HashSet<string> RedactTypes { get; set; } = new()
        { PiiTypes.Email, PiiTypes.Phone, PiiTypes.Tckn, PiiTypes.CreditCard, PiiTypes.Iban, PiiTypes.Dob };
    /// <summary>Redaction style: mask all, partial or hash.</summary>
    public RedactionStyle Redaction { get; set; } = RedactionStyle.MaskAll;

    /// <summary>
    /// Optional secret key for <see cref="RedactionStyle.Hash"/>. When null, a random per-process key is used.
    /// Set a stable secret to make hashes deterministic across instances (needed for cross-record correlation),
    /// but never expose it: the hash is only irreversible if the key stays secret.
    /// </summary>
    public string? RedactionHashKey { get; set; } = null;

    /// <summary>Body returned to the client when a request/response is blocked.</summary>
    public string BlockMessage { get; set; } = "Blocked by DataGuardian policy.";

    // Headers toggle
    /// <summary>Emit the <c>X-DataGuardian-*</c> headers. These reveal detected PII types; keep them internal.</summary>
    public bool EmitHeaders { get; set; } = true;

    /// <summary>Default region (ISO 3166-1 alpha-2) used to parse national-format phone numbers.</summary>
    public string DefaultPhoneRegion { get; set; } = "TR";

    /// <summary>Language override: null = auto-detect, otherwise <c>"tr"</c> or <c>"en"</c>.</summary>
    public string? LanguageOverride { get; set; } = null;

    /// <summary>Validates the configuration and throws <see cref="ArgumentException"/> on invalid values.</summary>
    public void Validate()
    {
        if (K <= 0)
            throw new ArgumentException("K must be greater than 0.", nameof(K));
        if (MaxCountPerType < 1)
            throw new ArgumentException("MaxCountPerType must be at least 1.", nameof(MaxCountPerType));
        if (MaxBodySizeBytes < 1)
            throw new ArgumentException("MaxBodySizeBytes must be positive.", nameof(MaxBodySizeBytes));
        if (BlockAt > 10)
            throw new ArgumentException("BlockAt cannot exceed 10.", nameof(BlockAt));
        if (RedactAt < 0 || RedactAt > 10)
            throw new ArgumentException("RedactAt must be between 0 and 10.", nameof(RedactAt));
        if (NerMaxSequenceLength < 1)
            throw new ArgumentException("NerMaxSequenceLength must be positive.", nameof(NerMaxSequenceLength));
        if (MinNerConfidence < 0 || MinNerConfidence > 1)
            throw new ArgumentException("MinNerConfidence must be between 0 and 1.", nameof(MinNerConfidence));
        if (string.IsNullOrWhiteSpace(HeaderPrefix))
            throw new ArgumentException("HeaderPrefix must not be empty.", nameof(HeaderPrefix));
    }
}

/// <summary>Action the middleware takes based on the risk score.</summary>
public enum ActionMode
{
    /// <summary>Do nothing.</summary>
    None,
    /// <summary>Emit headers only.</summary>
    Tag,
    /// <summary>Redact matching values in the response body.</summary>
    Redact,
    /// <summary>Return 403 when the risk threshold is exceeded.</summary>
    Block
}

/// <summary>How matched values are redacted.</summary>
public enum RedactionStyle
{
    /// <summary>Replace every character with <c>*</c>.</summary>
    MaskAll,
    /// <summary>Keep the first and last two characters (values of length &lt;= 4 are fully masked).</summary>
    Partial,
    /// <summary>Replace with a keyed HMAC-SHA256 prefix.</summary>
    Hash
}
