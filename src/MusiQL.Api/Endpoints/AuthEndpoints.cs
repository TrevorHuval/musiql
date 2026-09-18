using Microsoft.Extensions.Options;
using MusiQL.Api.Auth;
using MusiQL.Api.Contracts;
using MusiQL.Api.Errors;

namespace MusiQL.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/register", Register).RequireRateLimiting(RateLimits.Register);
        group.MapPost("/login", Login).RequireRateLimiting(RateLimits.Auth);
        group.MapPost("/refresh", Refresh).RequireRateLimiting(RateLimits.Auth);
        group.MapPost("/logout", Logout).RequireRateLimiting(RateLimits.Auth);
        return group;
    }

    private static async Task<IResult> Register(
        RegisterRequest request, AuthService auth, IOptions<LimitsOptions> limits, CancellationToken ct)
    {
        if (Credentials(request.Email, request.Password, limits.Value) is { } problem)
        {
            return problem;
        }

        var result = await auth.RegisterAsync(request.Email.Trim(), request.Password, ct);
        return result.Ok
            ? Results.Json(ToResponse(result.Tokens!), statusCode: StatusCodes.Status201Created)
            : ApiProblems.Auth(result);
    }

    private static async Task<IResult> Login(
        LoginRequest request, AuthService auth, IOptions<LimitsOptions> limits, CancellationToken ct)
    {
        if (Credentials(request.Email, request.Password, limits.Value) is { } problem)
        {
            return problem;
        }

        var result = await auth.LoginAsync(request.Email.Trim(), request.Password, ct);
        return result.Ok ? Results.Ok(ToResponse(result.Tokens!)) : ApiProblems.Auth(result);
    }

    private static async Task<IResult> Refresh(RefreshRequest request, AuthService auth, CancellationToken ct)
    {
        if (!TokenShaped(request.RefreshToken))
        {
            return ApiProblems.Auth(AuthResult.Fail(AuthFailure.InvalidCredentials));
        }

        var result = await auth.RefreshAsync(request.RefreshToken, ct);
        return result.Ok ? Results.Ok(ToResponse(result.Tokens!)) : ApiProblems.Auth(result);
    }

    private static async Task<IResult> Logout(LogoutRequest request, AuthService auth, CancellationToken ct)
    {
        if (TokenShaped(request.RefreshToken))
        {
            await auth.RevokeAsync(request.RefreshToken, ct);
        }

        return Results.NoContent();
    }

    private static IResult? Credentials(string? email, string? password, LimitsOptions limits)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return Results.Problem(
                title: "Email and password are required",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (email.Length > limits.MaxEmailLength || password.Length > limits.MaxPasswordLength)
        {
            return ApiProblems.Invalid(
                "Email or password is too long",
                $"Email is limited to {limits.MaxEmailLength} characters and password to {limits.MaxPasswordLength}.");
        }

        return null;
    }

    private static bool TokenShaped(string? token) =>
        !string.IsNullOrWhiteSpace(token) && token.Length <= TokenService.RefreshTokenLength;

    private static AuthResponse ToResponse(IssuedTokens tokens) => new(
        tokens.Access.Value,
        tokens.Access.ExpiresAt,
        tokens.RefreshToken,
        tokens.User.Id,
        tokens.User.Email ?? "");
}
