using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MusiQL.Api;
using MusiQL.Api.Auth;
using MusiQL.Api.Endpoints;
using MusiQL.Api.Errors;
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

builder.Services.Configure<LimitsOptions>(builder.Configuration.GetSection(LimitsOptions.Section));
var limits = builder.Configuration.GetSection(LimitsOptions.Section).Get<LimitsOptions>() ?? new LimitsOptions();

builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = limits.MaxRequestBodyBytes);

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

if (builder.Environment.IsProduction())
{
    ProductionConfig.Check(builder.Configuration, jwt);
}

builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddHostedService<RefreshTokenCleanup>();

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
builder.Services.AddSingleton<QueryGate>();
builder.Services.AddSingleton<GenreSelectivity>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GenreSelectivity>());
builder.Services.AddSingleton<OperationLocks>();
builder.Services.AddScoped<QueryService>();
builder.Services.AddScoped<PlaylistSnapshotService>();

builder.Services.AddSpotifyIntegration(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<BusyExceptionHandler>();
builder.Services.AddHttpLogging(options =>
    options.LoggingFields = HttpLoggingFields.RequestMethod
        | HttpLoggingFields.RequestPath
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.Duration);

// Behind a reverse proxy the client address arrives in X-Forwarded-For. The
// API only honours it when told how many proxy hops sit in front of it, and it
// is never published directly, so the immediate peer is always that proxy.
var trustedHops = builder.Configuration.GetValue<int>("Proxy:TrustedHops");
if (trustedHops > 0)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = trustedHops;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
        RateLimitPartition.GetFixedWindowLimiter(ClientAddress(http), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limits.RequestsPerTenSeconds,
            Window = TimeSpan.FromSeconds(10)
        }));

    options.AddPolicy(RateLimits.Query, http =>
        RateLimitPartition.GetFixedWindowLimiter(UserOrAddress(http), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limits.QueriesPerTenSeconds,
            Window = TimeSpan.FromSeconds(10)
        }));

    options.AddPolicy(RateLimits.Write, http =>
        RateLimitPartition.GetFixedWindowLimiter(UserOrAddress(http), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limits.WritesPerMinute,
            Window = TimeSpan.FromMinutes(1)
        }));

    options.AddPolicy(RateLimits.Auth, http =>
        RateLimitPartition.GetFixedWindowLimiter(ClientAddress(http), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limits.AuthPerMinute,
            Window = TimeSpan.FromMinutes(1)
        }));

    options.AddPolicy(RateLimits.Register, http =>
        RateLimitPartition.GetFixedWindowLimiter(ClientAddress(http), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limits.RegistrationsPerHour,
            Window = TimeSpan.FromHours(1)
        }));
});

const string devCors = "dev";
builder.Services.AddCors(options =>
    options.AddPolicy(devCors, policy => policy
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

if (trustedHops > 0)
{
    app.UseForwardedHeaders();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpLogging();

if (app.Environment.IsDevelopment())
{
    app.UseCors(devCors);
}

if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Startup:MigrateAppSchema"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/api/health", async (MusiQLDbContext db) =>
{
    var connected = await CanConnect(db);
    var catalogLoaded = connected && await db.Recordings.AnyAsync();
    var report = new HealthReport("musiql-api", connected, catalogLoaded);
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

static string ClientAddress(HttpContext http) =>
    http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

static string UserOrAddress(HttpContext http) =>
    http.User.UserId()?.ToString() ?? ClientAddress(http);

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
