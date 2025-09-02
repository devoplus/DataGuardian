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
}
