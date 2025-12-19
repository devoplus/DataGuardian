using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Devoplus.DataGuardian;

public sealed class DataGuardianMiddleware
{
    private readonly RequestDelegate _next;
    private readonly DataGuardianEngine _engine;
    private readonly DataGuardianOptions _opt;

    public DataGuardianMiddleware(RequestDelegate next, DataGuardianOptions opt)
    {
        _next = next;
        _opt = opt;

        Ner.INerRecognizer? ner = null;
        if (opt.EnableNer)
        {
            try { ner = new Ner.BertNerOnnx(opt); } catch { /* fall back */ }
        }
        _engine = new DataGuardianEngine(opt, ner);
    }

    public async Task Invoke(HttpContext ctx)
    {
        if (!IsAllowed(ctx))
        {
            await _next(ctx);
            return;
        }

        string? reqBody = null;
        if (_opt.AnalyzeRequests && IsTextContent(ctx.Request.ContentType))
            reqBody = await ReadRequestBodyAsync(ctx);

        Stream? originalBody = null;
        MemoryStream? buffer = null;
        if (_opt.AnalyzeResponses)
        {
            originalBody = ctx.Response.Body;
            buffer = new MemoryStream();
            ctx.Response.Body = buffer;
        }

        // Analyze request; possibly block
        if (reqBody != null)
        {
            var (r, counts, hits) = _engine.AnalyzeDetailed(reqBody);
            if (_opt.EmitHeaders)
            {
                ctx.Response.Headers[$"{_opt.HeaderPrefix}-Request-Risk"] = r.ToString("F2");
                ctx.Response.Headers[$"{_opt.HeaderPrefix}-Request-Detected"] = FormatCounts(counts);
            }

            if (_opt.Action == ActionMode.Block && _opt.BlockAt >= 0 && r >= _opt.BlockAt)
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsync("Blocked by DataGuardian policy.");
                return;
            }
        }

        await _next(ctx);

        // Analyze response; tag/redact/block
        if (_opt.AnalyzeResponses && buffer != null && originalBody != null)
        {
            buffer.Position = 0;
            var responseText = string.Empty;

            if (IsTextContent(ctx.Response.ContentType) && buffer.Length <= _opt.MaxBodySizeBytes)
            {
                using var reader = new StreamReader(buffer, Encoding.UTF8, leaveOpen: true);
                responseText = await reader.ReadToEndAsync();
                buffer.Position = 0;
            }

            if (!string.IsNullOrEmpty(responseText))
            {
                var (r, counts, hits) = _engine.AnalyzeDetailed(responseText);
                if (_opt.EmitHeaders)
                {
                    ctx.Response.Headers[$"{_opt.HeaderPrefix}-Response-Risk"] = r.ToString("F2");
                    ctx.Response.Headers[$"{_opt.HeaderPrefix}-Response-Detected"] = FormatCounts(counts);
                }

                if (_opt.Action == ActionMode.Block && _opt.BlockAt >= 0 && r >= _opt.BlockAt)
                {
                    ctx.Response.Clear();
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await ctx.Response.WriteAsync("Blocked by DataGuardian policy.");
                    return;
                }
                if (_opt.Action == ActionMode.Redact && r >= _opt.RedactAt && hits.Count > 0)
                {
                    var redacted = Redact(responseText, hits, _opt);
                    var bytes = Encoding.UTF8.GetBytes(redacted);
                    await originalBody.WriteAsync(bytes, 0, bytes.Length);
                    ctx.Response.Body = originalBody;
                    return;
                }
            }

            if (!_opt.AnalyzeResponses)
                ctx.Response.Headers["X-DataGuardian-Response-Skip-Reason"] = "AnalyzeResponses=false";
            else if (!IsTextContent(ctx.Response.ContentType))
                ctx.Response.Headers["X-DataGuardian-Response-Skip-Reason"] = "ContentTypeNotAnalyzable";
            else if (buffer.Length > _opt.MaxBodySizeBytes)
                ctx.Response.Headers["X-DataGuardian-Response-Skip-Reason"] = "BodyTooLarge";
            else if (string.IsNullOrEmpty(responseText))
                ctx.Response.Headers["X-DataGuardian-Response-Skip-Reason"] = "EmptyResponse";

            await buffer.CopyToAsync(originalBody);
            ctx.Response.Body = originalBody;
        }
    }

    private static string FormatCounts(Dictionary<string,int> counts)
        => string.Join(";", counts.Select(kv => $"{kv.Key}={kv.Value}"));

    private bool IsTextContent(string? contentType)
        => !string.IsNullOrEmpty(contentType) &&
           _opt.AnalyzableContentTypes.Any(ct => contentType.StartsWith(ct, StringComparison.OrdinalIgnoreCase));

    private static async Task<string> ReadRequestBodyAsync(HttpContext ctx)
    {
        ctx.Request.EnableBuffering();
        using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        ctx.Request.Body.Position = 0;
        return body;
    }

    private bool IsAllowed(HttpContext ctx)
    {
        var path = ctx.Request.Path.HasValue ? ctx.Request.Path.Value! : string.Empty;
        var method = ctx.Request.Method?.ToUpperInvariant() ?? "";

        if (_opt.IncludePaths.Count > 0 && !_opt.IncludePaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return false;
        if (_opt.ExcludePaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return false;
        if (_opt.IncludeMethods.Count > 0 && !_opt.IncludeMethods.Contains(method))
            return false;
        if (_opt.ExcludeMethods.Contains(method))
            return false;
        return true;
    }

    private static string Redact(string text, IEnumerable<PiiHit> hits, DataGuardianOptions opt)
    {
        if (opt.Redaction == RedactionStyle.JsonSafe)
        {
            return RedactJsonSafe(text, hits, opt);
        }

        var sb = new StringBuilder(text);
        var toRedact = hits.Where(h => opt.RedactTypes.Contains(h.Type)).OrderByDescending(h => h.Start).ToList();
        foreach (var h in toRedact)
        {
            if (h.Start < 0 || h.Start + h.Length > sb.Length) continue;
            if (opt.Redaction == RedactionStyle.MaskAll)
            {
                for (int i = 0; i < h.Length; i++) sb[h.Start + i] = '*';
            }
            else if (opt.Redaction == RedactionStyle.Partial)
            {
                for (int i = 0; i < h.Length; i++)
                    sb[h.Start + i] = (i < 2 || i >= h.Length - 2) ? sb[h.Start + i] : '*';
            }
            else // Hash
            {
                var segment = sb.ToString(h.Start, h.Length);
                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(segment)));
                var repl = hash[..12];
                sb.Remove(h.Start, h.Length);
                sb.Insert(h.Start, repl);
            }
        }
        return sb.ToString();
    }

    private static string RedactJsonSafe(string text, IEnumerable<PiiHit> hits, DataGuardianOptions opt)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(text);
            var toRedact = hits.Where(h => opt.RedactTypes.Contains(h.Type)).ToList();
            return RedactJsonElement(text, doc.RootElement, toRedact);
        }
        catch
        {
            // Fall back to regular redaction if JSON parsing fails
            return Redact(text, hits, new DataGuardianOptions { Redaction = RedactionStyle.Partial, RedactTypes = opt.RedactTypes });
        }
    }

    private static string RedactJsonElement(string originalText, System.Text.Json.JsonElement element, List<PiiHit> hits)
    {
        var sb = new StringBuilder(originalText);
        
        // Process hits in reverse order to maintain correct positions
        var sortedHits = hits.OrderByDescending(h => h.Start).ToList();
        
        foreach (var hit in sortedHits)
        {
            if (hit.Start < 0 || hit.Start + hit.Length > sb.Length) continue;
            
            // Check if this hit is within a JSON value (not a key)
            if (IsWithinJsonValue(originalText, hit.Start, element))
            {
                // Apply partial redaction to preserve some readability
                var value = sb.ToString(hit.Start, hit.Length);
                string redacted;
                
                if (hit.Length <= 3)
                {
                    redacted = new string('*', hit.Length);
                }
                else if (value.Contains('@')) // Email-like
                {
                    var atPos = value.IndexOf('@');
                    var parts = value.Split('@');
                    if (parts.Length == 2)
                    {
                        var localPart = parts[0].Length > 2 ? parts[0][..1] + new string('*', parts[0].Length - 1) : new string('*', parts[0].Length);
                        var domainParts = parts[1].Split('.');
                        var domain = domainParts.Length > 1 
                            ? new string('*', domainParts[0].Length) + "." + domainParts[^1]
                            : new string('*', parts[1].Length);
                        redacted = localPart + "@" + domain;
                    }
                    else
                    {
                        redacted = value[..1] + new string('*', value.Length - 1);
                    }
                }
                else // Partial masking
                {
                    var visibleChars = Math.Min(2, hit.Length / 3);
                    redacted = value[..visibleChars] + new string('*', hit.Length - 2 * visibleChars) + value[^visibleChars..];
                }
                
                sb.Remove(hit.Start, hit.Length);
                sb.Insert(hit.Start, redacted);
            }
        }
        
        return sb.ToString();
    }

    private static bool IsWithinJsonValue(string json, int position, System.Text.Json.JsonElement root)
    {
        // Simple heuristic: check if the position is not immediately after a colon and quote
        // This is a simplified approach - we assume the position is in a value if it's not clearly a key
        
        // Look backward to find the nearest structural character
        int i = position - 1;
        while (i >= 0 && char.IsWhiteSpace(json[i])) i--;
        
        if (i < 0) return false;
        
        // If we find a colon before finding a comma/bracket, we're likely in a value
        int colonPos = -1;
        int commaOrBracketPos = -1;
        
        for (int j = i; j >= 0 && j > Math.Max(0, position - 100); j--)
        {
            if (json[j] == ':' && colonPos < 0) colonPos = j;
            if ((json[j] == ',' || json[j] == '{' || json[j] == '[') && commaOrBracketPos < 0) commaOrBracketPos = j;
            
            if (colonPos >= 0 && commaOrBracketPos >= 0) break;
        }
        
        // If we found a colon more recently than a comma/bracket, we're in a value
        return colonPos > commaOrBracketPos;
    }
}