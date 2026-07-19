using MusiQL.Api;
using MusiQL.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMusiQLData(
    builder.Configuration.GetConnectionString("MusiQL")
        ?? throw new InvalidOperationException("Connection string 'MusiQL' is not configured."));

const string devCors = "dev";
builder.Services.AddCors(options =>
    options.AddPolicy(devCors, policy => policy
        .WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseCors(devCors);
}

app.MapGet("/api/health", async (MusiQLDbContext db) =>
{
    var connected = await CanConnect(db);
    var report = new HealthReport("musiql-api", connected);
    return Results.Json(
        report,
        statusCode: connected ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});

app.Run();

static async Task<bool> CanConnect(MusiQLDbContext db)
{
    try
    {
        return await db.Database.CanConnectAsync();
    }
    catch
    {
        return false;
    }
}
