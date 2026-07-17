using Devoplus.DataGuardian;

var builder = WebApplication.CreateBuilder(args);

// Bind DataGuardian options from the "DataGuardian" section of appsettings.json.
// (You can also configure inline: builder.Services.AddDataGuardian(o => { o.Action = ActionMode.Tag; });)
builder.Services.AddDataGuardian(builder.Configuration.GetSection("DataGuardian"));

var app = builder.Build();

// Resolves the options registered above.
app.UseDataGuardian();

app.MapPost("/echo", async (HttpContext ctx) =>
{
    using var sr = new StreamReader(ctx.Request.Body);
    var text = await sr.ReadToEndAsync();
    return Results.Text(text, "application/json");
});

app.Run();
