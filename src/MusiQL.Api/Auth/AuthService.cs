using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MusiQL.Data.App;

namespace MusiQL.Api.Auth;

public enum AuthFailure
{
    Validation,
    InvalidCredentials,
    LockedOut
}

public sealed record IssuedTokens(AppUser User, AccessToken Access, string RefreshToken);

public sealed record AuthResult(IssuedTokens? Tokens, AuthFailure? Failure, IReadOnlyList<string> Messages)
{
    public bool Ok => Tokens is not null;

    public static AuthResult Success(IssuedTokens tokens) => new(tokens, null, []);

    public static AuthResult Fail(AuthFailure failure, params string[] messages) =>
        new(null, failure, messages);
}

public sealed class AuthService(
    UserManager<AppUser> users,
    AppDbContext db,
    TokenService tokens,
    IOptions<JwtOptions> jwt)
{
    public async Task<AuthResult> RegisterAsync(string email, string password, CancellationToken ct)
    {
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            LockoutEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            return AuthResult.Fail(AuthFailure.Validation, created.Errors.Select(e => e.Description).ToArray());
        }

        return AuthResult.Success(await IssueAsync(user, ct));
    }

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            return AuthResult.Fail(AuthFailure.InvalidCredentials);
        }

        if (await users.IsLockedOutAsync(user))
        {
            return AuthResult.Fail(AuthFailure.LockedOut);
        }

        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.AccessFailedAsync(user);
            return await users.IsLockedOutAsync(user)
                ? AuthResult.Fail(AuthFailure.LockedOut)
                : AuthResult.Fail(AuthFailure.InvalidCredentials);
        }

        await users.ResetAccessFailedCountAsync(user);
        return AuthResult.Success(await IssueAsync(user, ct));
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = TokenService.Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        var now = DateTime.UtcNow;
        if (stored is null || !stored.Active(now))
        {
            return AuthResult.Fail(AuthFailure.InvalidCredentials);
        }

        var user = await users.FindByIdAsync(stored.UserId.ToString());
        if (user is null)
        {
            return AuthResult.Fail(AuthFailure.InvalidCredentials);
        }

        stored.RevokedAt = now;
        return AuthResult.Success(await IssueAsync(user, ct));
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct)
    {
        var hash = TokenService.Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is not null && stored.RevokedAt is null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task<IssuedTokens> IssueAsync(AppUser user, CancellationToken ct)
    {
        var access = tokens.CreateAccessToken(user);
        var refresh = TokenService.CreateRefreshToken();
        var now = DateTime.UtcNow;

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            TokenHash = TokenService.Hash(refresh),
            CreatedAt = now,
            ExpiresAt = now.AddDays(jwt.Value.RefreshTokenDays)
        });
        await db.SaveChangesAsync(ct);

        return new IssuedTokens(user, access, refresh);
    }
}
