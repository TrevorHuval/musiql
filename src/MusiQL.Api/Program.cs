using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MusiQL.Api;
using MusiQL.Api.Auth;
using MusiQL.Api.Endpoints;
using MusiQL.Api.Query;
using MusiQL.Api.Spotify;
using MusiQL.Core.Mql;
using MusiQL.Data;
using MusiQL.Data.App;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("MusiQL")
    ?? throw new InvalidOperationException("Connection string 'MusiQL' is not configured.");

builder.Services.AddMusiQLData(connectionString);
builder.Services.AddMusiQLApp(connectionString);

builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.SigningKey))
{
    throw new InvalidOperationException("Jwt:SigningKey is not configured.");
}

builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<AuthService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton(MqlEngine.CreateDefault());
builder.Services.Configure<QueryOptions>(builder.Configuration.GetSection("Query"));
builder.Services.PostConfigure<QueryOptions>(options =>
    options.ConnectionString = builder.Configuration.GetConnectionString("Query") ?? connectionString);
builder.Services.AddScoped<QueryService>();
builder.Services.AddScoped<PlaylistSnapshotService>();

builder.Services.AddSpotifyIntegration(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddHttpLogging(options =>
    options.LoggingFields = HttpLoggingFields.RequestMethod
        | HttpLoggingFields.RequestPath
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.Duration);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimits.Query, http =>
    {
        var key = http.User.UserId()?.ToString()
            ?? http.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromSeconds(10)
        });
    });
});

const string devCors = "dev";
builder.Services.AddCors(options =>
    options.AddPolicy(devCors, policy => policy
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpLogging();

if (app.Environment.IsDevelopment())
{
    app.UseCors(devCors);
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/api/health", async (MusiQLDbContext db) =>
{
    var connected = await CanConnect(db);
    var report = new HealthReport("musiql-api", connected);
    return Results.Json(
        report,
        statusCode: connected ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});

var api = app.MapGroup("/api");
api.MapGroup("/auth").MapAuthEndpoints();
api.MapGroup("/playlists").MapPlaylistEndpoints().RequireAuthorization();
api.MapGroup("/query").MapQueryEndpoints().RequireAuthorization();
api.MapGroup("/catalog").MapCatalogEndpoints().RequireAuthorization();
api.MapGroup("/spotify").MapSpotifyEndpoints().RequireAuthorization();

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

public partial class Program;
