using System.Text;
using MusiQL.Api.Auth;
using MusiQL.Api.Spotify;

namespace MusiQL.Api;

// Startup guards that only apply outside development: secrets must be real and
// MQL must run over the read-only role unless the operator opts out explicitly.
public static class ProductionConfig
{
    private const int MinKeyBytes = 32;

    private static readonly string[] Placeholders = ["change-me", "changeme", "example", "secret", "password"];

    public static void Check(IConfiguration configuration, JwtOptions jwt)
    {
        RequireStrong("Jwt:SigningKey", jwt.SigningKey);

        var spotify = configuration.GetSection(SpotifyOptions.Section).Get<SpotifyOptions>() ?? new SpotifyOptions();
        if (spotify.Configured)
        {
            RequireStrong("Spotify:TokenEncryptionKey", spotify.TokenEncryptionKey);
        }

        var queryConnection = configuration.GetConnectionString("Query");
        if (string.IsNullOrWhiteSpace(queryConnection)
            && !configuration.GetValue<bool>("Query:AllowOwnerConnection"))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Query is not configured. MQL would run over the owner connection. "
                + "Create the read-only role (src/MusiQL.Data/Sql/query_role.sql) and set the connection string, "
                + "or set Query:AllowOwnerConnection=true to accept the risk.");
        }
    }

    private static void RequireStrong(string key, string value)
    {
        if (Encoding.UTF8.GetByteCount(value) < MinKeyBytes)
        {
            throw new InvalidOperationException($"{key} must be at least {MinKeyBytes} bytes.");
        }

        var lowered = value.ToLowerInvariant();
        if (Placeholders.Any(lowered.Contains))
        {
            throw new InvalidOperationException($"{key} looks like a placeholder value. Generate a random key.");
        }
    }
}
