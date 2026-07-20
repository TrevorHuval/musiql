using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MusiQL.Api.Contracts;
using MusiQL.Data.App;

namespace MusiQL.Api.Spotify;

public sealed class SpotifyAuthService(
    AppDbContext db,
    SpotifyTokenClient tokenClient,
    ITokenProtector protector,
    IHttpClientFactory httpFactory,
    IOptions<SpotifyOptions> options)
{
    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);

    public SpotifyOptions Options => options.Value;

    public async Task<string> BuildConnectUrlAsync(Guid userId, CancellationToken ct)
    {
        EnsureConfigured();
        var (verifier, challenge) = Pkce.Create();
        var state = Pkce.NewState();
        var now = DateTime.UtcNow;

        var replaced = await db.SpotifyAuthStates
            .Where(s => s.UserId == userId || s.ExpiresAt < now)
            .ToListAsync(ct);
        db.SpotifyAuthStates.RemoveRange(replaced);
        db.SpotifyAuthStates.Add(new SpotifyAuthState
        {
            State = state,
            UserId = userId,
            CodeVerifier = verifier,
            RedirectUri = Options.RedirectUri,
            ExpiresAt = now + StateTtl
        });
        await db.SaveChangesAsync(ct);

        var query = new Dictionary<string, string>
        {
            ["client_id"] = Options.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = Options.RedirectUri,
            ["code_challenge_method"] = "S256",
            ["code_challenge"] = challenge,
            ["state"] = state,
            ["scope"] = Options.Scopes
        };
        var qs = string.Join("&", query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        return $"https://accounts.spotify.com/authorize?{qs}";
    }

    public async Task<SpotifyConnectionStatus> CompleteAsync(
        Guid userId, string state, string code, CancellationToken ct)
    {
        EnsureConfigured();
        var stored = await db.SpotifyAuthStates.FirstOrDefaultAsync(s => s.State == state, ct);
        if (stored is null || stored.UserId != userId || stored.ExpiresAt < DateTime.UtcNow)
        {
            throw new SpotifyAuthStateException();
        }

        db.SpotifyAuthStates.Remove(stored);

        var tokens = await tokenClient.ExchangeCodeAsync(code, stored.CodeVerifier, stored.RedirectUri, ct);
        var client = new SpotifyUserClient(httpFactory.CreateClient(SpotifyClientFactory.HttpClientName), tokens.AccessToken);
        var profile = await client.GetProfileAsync(ct);

        var now = DateTime.UtcNow;
        var account = await db.SpotifyAccounts.FirstOrDefaultAsync(a => a.UserId == userId, ct);
        if (account is null)
        {
            account = new SpotifyAccount { UserId = userId, ConnectedAt = now };
            db.SpotifyAccounts.Add(account);
        }

        account.SpotifyUserId = profile.Id;
        account.DisplayName = profile.DisplayName;
        ApplyTokens(account, tokens);
        await db.SaveChangesAsync(ct);

        return Status(account);
    }

    public async Task DisconnectAsync(Guid userId, CancellationToken ct)
    {
        var account = await db.SpotifyAccounts.FirstOrDefaultAsync(a => a.UserId == userId, ct);
        if (account is not null)
        {
            db.SpotifyAccounts.Remove(account);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<SpotifyConnectionStatus> GetStatusAsync(Guid userId, CancellationToken ct)
    {
        var account = await db.SpotifyAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == userId, ct);
        return account is null
            ? new SpotifyConnectionStatus(false, null, null, null, null, 0, 0)
            : Status(account);
    }

    public async Task<SpotifyAccessContext> GetAccessTokenAsync(Guid userId, CancellationToken ct)
    {
        EnsureConfigured();
        var account = await db.SpotifyAccounts.FirstOrDefaultAsync(a => a.UserId == userId, ct)
            ?? throw new SpotifyNotConnectedException();

        if (account.AccessExpiresAt > DateTime.UtcNow + RefreshMargin)
        {
            return new SpotifyAccessContext(protector.Unprotect(account.AccessTokenCipher), account.SpotifyUserId);
        }

        var refreshToken = protector.Unprotect(account.RefreshTokenCipher);
        var tokens = await tokenClient.RefreshAsync(refreshToken, ct);
        ApplyTokens(account, tokens);
        await db.SaveChangesAsync(ct);
        return new SpotifyAccessContext(tokens.AccessToken, account.SpotifyUserId);
    }

    private void ApplyTokens(SpotifyAccount account, SpotifyTokens tokens)
    {
        var now = DateTime.UtcNow;
        account.AccessTokenCipher = protector.Protect(tokens.AccessToken);
        account.AccessExpiresAt = now.AddSeconds(tokens.ExpiresIn);
        account.UpdatedAt = now;
        if (!string.IsNullOrEmpty(tokens.RefreshToken))
        {
            account.RefreshTokenCipher = protector.Protect(tokens.RefreshToken);
        }

        if (!string.IsNullOrEmpty(tokens.Scope))
        {
            account.Scopes = tokens.Scope;
        }
    }

    private void EnsureConfigured()
    {
        if (!Options.Configured)
        {
            throw new SpotifyNotConfiguredException();
        }
    }

    private static SpotifyConnectionStatus Status(SpotifyAccount account) => new(
        true,
        account.DisplayName,
        account.SpotifyUserId,
        account.ConnectedAt,
        account.LibrarySyncedAt,
        account.LibrarySavedCount,
        account.LibraryMatchedCount);
}
