using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Devoplus.DataGuardian;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Devoplus.DataGuardian.Tests;

public class MiddlewareTests
{
    // Echo endpoint: writes the request body back as JSON.
    private static async Task<IHost> StartEchoAsync(DataGuardianOptions opt)
    {
        return await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.Configure(app =>
                {
                    app.UseDataGuardian(opt);
                    app.Run(async ctx =>
                    {
                        ctx.Response.ContentType = "application/json; charset=utf-8";
                        using var sr = new System.IO.StreamReader(ctx.Request.Body);
                        var body = await sr.ReadToEndAsync();
                        await ctx.Response.WriteAsync(body);
                    });
                });
            })
            .StartAsync();
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    [Fact]
    public async Task Tag_Mode_Emits_Response_Headers()
    {
        using var host = await StartEchoAsync(new DataGuardianOptions { Action = ActionMode.Tag });
        var client = host.GetTestClient();

        var resp = await client.PostAsync("/", Json("{\"email\":\"a@b.com\"}"));

        Assert.True(resp.Headers.TryGetValues("X-DataGuardian-Response-Detected", out var detected));
        Assert.Contains("EMAIL", string.Join(";", detected));
    }

    [Fact]
    public async Task Risk_Header_Uses_Invariant_Decimal_Point()
    {
        var prev = CultureInfo.DefaultThreadCurrentCulture;
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("tr-TR");
        try
        {
            using var host = await StartEchoAsync(new DataGuardianOptions { Action = ActionMode.Tag });
            var client = host.GetTestClient();

            var resp = await client.PostAsync("/", Json("{\"iban\":\"TR330006100519786457841326\"}"));

            Assert.True(resp.Headers.TryGetValues("X-DataGuardian-Response-Risk", out var risk));
            var value = string.Join("", risk);
            Assert.DoesNotContain(",", value); // must not be culture-formatted "6,99"
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentCulture = prev;
        }
    }

    [Fact]
    public async Task Block_Mode_Returns_403_With_Body()
    {
        using var host = await StartEchoAsync(new DataGuardianOptions { Action = ActionMode.Block, BlockAt = 1 });
        var client = host.GetTestClient();

        var resp = await client.PostAsync("/", Json("{\"tckn\":\"10000000146\"}"));

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrEmpty(body));           // regression: body was previously lost
        Assert.Contains("Blocked", body);
    }

    [Fact]
    public async Task Block_Response_Strips_Detection_Header()
    {
        using var host = await StartEchoAsync(new DataGuardianOptions { Action = ActionMode.Block, BlockAt = 1 });
        var client = host.GetTestClient();

        var resp = await client.PostAsync("/", Json("{\"tckn\":\"10000000146\"}"));

        Assert.False(resp.Headers.Contains("X-DataGuardian-Request-Detected"));
    }

    [Fact]
    public async Task Redact_MaskAll_Removes_Value_And_Sets_ContentLength()
    {
        using var host = await StartEchoAsync(new DataGuardianOptions
        {
            Action = ActionMode.Redact,
            RedactAt = 0,
            Redaction = RedactionStyle.MaskAll
        });
        var client = host.GetTestClient();

        var resp = await client.PostAsync("/", Json("{\"email\":\"secret@example.com\"}"));
        var body = await resp.Content.ReadAsStringAsync();

        Assert.DoesNotContain("secret@example.com", body);
        Assert.Contains("*", body);
        Assert.Equal(Encoding.UTF8.GetByteCount(body), resp.Content.Headers.ContentLength);
    }

    [Fact]
    public async Task Redact_Hash_Changes_Length_Consistently()
    {
        using var host = await StartEchoAsync(new DataGuardianOptions
        {
            Action = ActionMode.Redact,
            RedactAt = 0,
            Redaction = RedactionStyle.Hash
        });
        var client = host.GetTestClient();

        var resp = await client.PostAsync("/", Json("{\"card\":\"4111111111111111\"}"));
        var body = await resp.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);           // regression: Content-Length mismatch crashed
        Assert.DoesNotContain("4111111111111111", body);
        Assert.Equal(Encoding.UTF8.GetByteCount(body), resp.Content.Headers.ContentLength);
    }

    [Fact]
    public async Task ExcludePaths_Skips_Processing()
    {
        using var host = await StartEchoAsync(new DataGuardianOptions
        {
            Action = ActionMode.Tag,
            ExcludePaths = { "/health" }
        });
        var client = host.GetTestClient();

        var resp = await client.PostAsync("/health", Json("{\"email\":\"a@b.com\"}"));

        Assert.False(resp.Headers.Contains("X-DataGuardian-Response-Detected"));
    }

    [Fact]
    public async Task Method_Filter_Is_Case_Insensitive()
    {
        using var host = await StartEchoAsync(new DataGuardianOptions
        {
            Action = ActionMode.Tag,
            IncludeMethods = { "post" }   // lower-case config must still match a POST request
        });
        var client = host.GetTestClient();

        var resp = await client.PostAsync("/", Json("{\"email\":\"a@b.com\"}"));

        Assert.True(resp.Headers.Contains("X-DataGuardian-Response-Detected"));
    }

    [Fact]
    public async Task Request_Body_Is_Analyzed()
    {
        using var host = await StartEchoAsync(new DataGuardianOptions { Action = ActionMode.Tag });
        var client = host.GetTestClient();

        var resp = await client.PostAsync("/", Json("{\"tckn\":\"10000000146\"}"));

        Assert.True(resp.Headers.TryGetValues("X-DataGuardian-Request-Detected", out var detected));
        Assert.Contains("TCKN", string.Join(";", detected));
    }
}
