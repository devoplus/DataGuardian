using Devoplus.DataGuardian;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var opt = new DataGuardianOptions
{
    AnalyzeRequests = true,
    AnalyzeResponses = true,
    HeaderPrefix = "X-DataGuardian",
    Action = ActionMode.Tag,
    LanguageOverride = "tr",
    BlockAt = -1,
    EnableNer = false,
};

app.UseDataGuardian(opt);

app.MapPost("/echo", async (HttpContext ctx) =>
{
    using var sr = new StreamReader(ctx.Request.Body);
    var text = await sr.ReadToEndAsync();
    return Results.Text(text, "application/json");
});

app.Run();
