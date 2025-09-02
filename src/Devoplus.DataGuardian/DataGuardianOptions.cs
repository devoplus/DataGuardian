using System;
using System.Collections.Generic;

namespace Devoplus.DataGuardian;

public sealed class DataGuardianOptions
{
    public HashSet<string> AnalyzableContentTypes { get; set; } =
        new(StringComparer.OrdinalIgnoreCase)
        { "application/json", "text/plain", "application/xml", "application/x-www-form-urlencoded" };

    public Dictionary<string, double> Weights { get; set; } = new()
    {
        ["TCKN"] = 10, ["CREDIT_CARD"] = 9, ["IBAN_TR"] = 8,
        ["DOB"] = 7, ["ADDRESS"] = 6, ["PHONE"] = 5, ["EMAIL"] = 4, ["PERSON"] = 3
    };

    public int MaxCountPerType { get; set; } = 5;
    public double K { get; set; } = 0.15;
    public bool AnalyzeRequests { get; set; } = true;
    public bool AnalyzeResponses { get; set; } = true;
    public double BlockAt { get; set; } = -1;

    public int MaxBodySizeBytes { get; set; } = 512 * 1024;

    // NER (optional)
    public bool EnableNer { get; set; } = false;
    public string NerModelPath { get; set; } = "models/kvkk-ner.onnx";
    public string NerTokenizerPath { get; set; } = "models/tokenizer.json";
    public string NerLabelsPath { get; set; } = "models/labels.txt";
    public int NerMaxSequenceLength { get; set; } = 256;
    public double MinNerConfidence { get; set; } = 0.6;

    // Header prefix (e.g., X-DataGuardian)
    public string HeaderPrefix { get; set; } = "X-DataGuardian";

    // Path & method filters
    public List<string> IncludePaths { get; set; } = new();
    public List<string> ExcludePaths { get; set; } = new();
    public List<string> IncludeMethods { get; set; } = new(); // POST, PUT
    public List<string> ExcludeMethods { get; set; } = new();

    // Entity filters
    public HashSet<string> IncludeEntityTypes { get; set; } = new(); // empty = all
    public HashSet<string> ExcludeEntityTypes { get; set; } = new();

    // Action mode
    public ActionMode Action { get; set; } = ActionMode.Tag; // Tag by default
    public double RedactAt { get; set; } = 0; // Redact when risk >= RedactAt
    public HashSet<string> RedactTypes { get; set; } = new() { "EMAIL","PHONE","TCKN","CREDIT_CARD","IBAN_TR","DOB" };
    public RedactionStyle Redaction { get; set; } = RedactionStyle.MaskAll;

    // Headers toggle
    public bool EmitHeaders { get; set; } = true;

    // Language override (null=auto, "tr" or "en")
    public string? LanguageOverride { get; set; } = null;
}

// Supporting enums
public enum ActionMode { None, Tag, Redact, Block }
public enum RedactionStyle { MaskAll, Partial, Hash }
