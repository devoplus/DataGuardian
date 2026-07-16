using Devoplus.DataGuardian.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Devoplus.DataGuardian;

/// <summary>
/// ASP.NET Core middleware that scores request/response bodies for PII and optionally tags, redacts or blocks.
/// </summary>
public sealed class DataGuardianMiddleware
{
    private static readonly byte[] DefaultHashKey = RandomNumberGenerator.GetBytes(32);

    private readonly RequestDelegate _next;
    private readonly DataGuardianEngine _engine;
    private readonly DataGuardianOptions _opt;

    public DataGuardianMiddleware(RequestDelegate next, DataGuardianOptions opt)
    {
        _next = next;
        _opt = opt ?? throw new ArgumentNullException(nameof(opt));
        _opt.Validate();

        Ner.INerRecognizer? ner = null;
        if (_opt.EnableNer)
        {
            try { ner = new Ner.BertNerOnnx(_opt); }
            catch { ner = null; /* NER stays disabled; see docs/security.md */ }
        }
        _engine = new DataGuardianEngine(_opt, ner);
    }

    public async Task Invoke(HttpContext ctx)
    {
        if (!IsAllowed(ctx))
        {
            await _next(ctx);
            return;
        }

        // ---------------- Request analysis (before the response body is swapped) ----------------
        if (_opt.AnalyzeRequests && IsTextContent(ctx.Request.ContentType))
        {
            var (reqBody, oversize) = await ReadRequestBodyAsync(ctx);
            if (oversize)
            {
                if (_opt.EmitHeaders)
                    ctx.Response.Headers[$"{_opt.HeaderPrefix}-Request-Skip-Reason"] = "BodyTooLarge";
                if (_opt.BlockOversizeBodies && _opt.Action == ActionMode.Block)
                {
                    await WriteBlockAsync(ctx);
                    return;
                }
            }
            else if (reqBody is not null)
            {
                var (risk, counts, _) = _engine.AnalyzeDetailed(reqBody);
                if (_opt.EmitHeaders)
                {
                    ctx.Response.Headers[$"{_opt.HeaderPrefix}-Request-Risk"] = Fmt(risk);
                    ctx.Response.Headers[$"{_opt.HeaderPrefix}-Request-Detected"] = FormatCounts(counts);
                }
                if (ShouldBlock(risk))
                {
                    await WriteBlockAsync(ctx);
                    return;
                }
            }
        }

        if (!_opt.AnalyzeResponses)
        {
            await _next(ctx);
            return;
        }

        // ---------------- Response analysis ----------------
        var originalBody = ctx.Response.Body;
        var capture = new ResponseCaptureStream(originalBody, _opt.MaxBodySizeBytes, () => IsTextContent(ctx.Response.ContentType));
        ctx.Response.Body = capture;
        try
        {
            await _next(ctx);

            // If the body was streamed straight through (non-text or oversize), it is already on the wire.
            if (capture.DidPassthrough)
            {
                if (_opt.EmitHeaders && capture.Overflowed && !ctx.Response.HasStarted)
                    ctx.Response.Headers[$"{_opt.HeaderPrefix}-Response-Skip-Reason"] = "BodyTooLarge";
                return;
            }

            var buffered = capture.GetBufferedBytes();
            var responseText = TryDecodeText(ctx.Response.ContentType, buffered, out var skipReason);

            if (responseText is null)
            {
                if (_opt.EmitHeaders && skipReason is not null)
                    ctx.Response.Headers[$"{_opt.HeaderPrefix}-Response-Skip-Reason"] = skipReason;
                await originalBody.WriteAsync(buffered);
                return;
            }

            var (risk, counts, hits) = _engine.AnalyzeDetailed(responseText);
            if (_opt.EmitHeaders)
            {
                ctx.Response.Headers[$"{_opt.HeaderPrefix}-Response-Risk"] = Fmt(risk);
                ctx.Response.Headers[$"{_opt.HeaderPrefix}-Response-Detected"] = FormatCounts(counts);
            }

            if (ShouldBlock(risk))
            {
                ctx.Response.Body = originalBody;
                await WriteBlockAsync(ctx);
                return;
            }

            if (_opt.Action == ActionMode.Redact && risk >= _opt.RedactAt && hits.Count > 0)
            {
                var redactable = hits.Where(h => _opt.RedactTypes.Contains(h.Type)).ToList();
                if (redactable.Count > 0)
                {
                    var redacted = Redact(responseText, redactable, _opt);
                    var bytes = Encoding.UTF8.GetBytes(redacted);
                    ctx.Response.ContentLength = bytes.Length;
                    await originalBody.WriteAsync(bytes);
                    return;
                }
                if (_opt.EmitHeaders)
                    ctx.Response.Headers[$"{_opt.HeaderPrefix}-Response-Skip-Reason"] = "NoRedactableTypes";
            }

            await originalBody.WriteAsync(buffered);
        }
        finally
        {
            ctx.Response.Body = originalBody;
            capture.Dispose();
        }
    }

    private async Task WriteBlockAsync(HttpContext ctx)
    {
        if (!ctx.Response.HasStarted)
        {
            ctx.Response.Clear();
            // Do not leak which PII types were found on a blocked response.
            ctx.Response.Headers.Remove($"{_opt.HeaderPrefix}-Request-Detected");
            ctx.Response.Headers.Remove($"{_opt.HeaderPrefix}-Response-Detected");
        }
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        ctx.Response.ContentType = "text/plain; charset=utf-8";
        await ctx.Response.WriteAsync(_opt.BlockMessage);
    }

    private string Fmt(double risk) => risk.ToString("F2", CultureInfo.InvariantCulture);

    private static string FormatCounts(Dictionary<string, int> counts)
        => string.Join(";", counts.Select(kv => $"{kv.Key}={kv.Value}"));

    private bool ShouldBlock(double risk)
        => _opt.Action == ActionMode.Block && _opt.BlockAt >= 0 && risk >= _opt.BlockAt;

    private bool IsTextContent(string? contentType)
    {
        // A missing/empty content type is treated as analyzable so it cannot be used to bypass the control.
        if (string.IsNullOrEmpty(contentType)) return true;
        return _opt.AnalyzableContentTypes.Any(ct => contentType.StartsWith(ct, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Reads the request body up to the size cap. Returns (body, oversize); body is null when skipped.</summary>
    private async Task<(string? body, bool oversize)> ReadRequestBodyAsync(HttpContext ctx)
    {
        ctx.Request.EnableBuffering();
        var stream = ctx.Request.Body;

        if (ctx.Request.ContentLength is long declared && declared > _opt.MaxBodySizeBytes)
        {
            stream.Position = 0;
            return (null, true);
        }

        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        long total = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            total += read;
            if (total > _opt.MaxBodySizeBytes)
            {
                stream.Position = 0;
                return (null, true);
            }
            ms.Write(buffer, 0, read);
        }

        stream.Position = 0;
        return (Encoding.UTF8.GetString(ms.ToArray()), false);
    }

    /// <summary>Decodes bytes to text when safe; otherwise returns null and sets a skip reason.</summary>
    private string? TryDecodeText(string? contentType, byte[] bytes, out string? skipReason)
    {
        skipReason = null;
        if (bytes.Length == 0) { skipReason = "EmptyResponse"; return null; }
        if (!IsTextContent(contentType)) { skipReason = "ContentTypeNotAnalyzable"; return null; }
        if (!IsUtf8Compatible(contentType)) { skipReason = "UnsupportedCharset"; return null; }
        try { return Encoding.UTF8.GetString(bytes); }
        catch { skipReason = "DecodeError"; return null; }
    }

    private static bool IsUtf8Compatible(string? contentType)
    {
        // Only decode/redact when the charset is UTF-8/ASCII (or unspecified). Re-encoding another
        // charset as UTF-8 would corrupt the body, so those are skipped rather than mangled.
        if (string.IsNullOrEmpty(contentType)) return true;
        if (!MediaTypeHeaderValue.TryParse(contentType, out var mt) || !mt.Charset.HasValue) return true;
        var cs = mt.Charset.ToString().Trim('"').ToLowerInvariant();
        return cs is "utf-8" or "utf8" or "us-ascii" or "ascii" or "";
    }

    private bool IsAllowed(HttpContext ctx)
    {
        var path = ctx.Request.Path.HasValue ? ctx.Request.Path.Value! : string.Empty;
        var method = ctx.Request.Method ?? string.Empty;

        if (_opt.IncludePaths.Count > 0 && !_opt.IncludePaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return false;
        if (_opt.ExcludePaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return false;
        if (_opt.IncludeMethods.Count > 0 && !_opt.IncludeMethods.Any(m => string.Equals(m, method, StringComparison.OrdinalIgnoreCase)))
            return false;
        if (_opt.ExcludeMethods.Any(m => string.Equals(m, method, StringComparison.OrdinalIgnoreCase)))
            return false;
        return true;
    }

    private static string Redact(string text, IEnumerable<PiiHit> hits, DataGuardianOptions opt)
    {
        var sb = new StringBuilder(text);
        var toRedact = hits.Where(h => opt.RedactTypes.Contains(h.Type)).OrderByDescending(h => h.Start).ToList();
        foreach (var h in toRedact)
        {
            if (h.Start < 0 || h.Start + h.Length > sb.Length || h.Length <= 0) continue;

            if (opt.Redaction == RedactionStyle.MaskAll)
            {
                for (int i = 0; i < h.Length; i++) sb[h.Start + i] = '*';
            }
            else if (opt.Redaction == RedactionStyle.Partial)
            {
                if (h.Length <= 4)
                {
                    for (int i = 0; i < h.Length; i++) sb[h.Start + i] = '*';
                }
                else
                {
                    for (int i = 0; i < h.Length; i++)
                        if (!(i < 2 || i >= h.Length - 2)) sb[h.Start + i] = '*';
                }
            }
            else // Hash
            {
                var segment = sb.ToString(h.Start, h.Length);
                var repl = HashValue(segment, opt);
                sb.Remove(h.Start, h.Length);
                sb.Insert(h.Start, repl);
            }
        }
        return sb.ToString();
    }

    private static string HashValue(string value, DataGuardianOptions opt)
    {
        var key = string.IsNullOrEmpty(opt.RedactionHashKey)
            ? DefaultHashKey
            : Encoding.UTF8.GetBytes(opt.RedactionHashKey);
        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..16];
    }
}
