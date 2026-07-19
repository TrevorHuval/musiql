using MusiQL.Api.Auth;
using MusiQL.Api.Contracts;
using MusiQL.Api.Errors;

namespace MusiQL.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/register", Register);
        group.MapPost("/login", Login);
        group.MapPost("/refresh", Refresh);
        group.MapPost("/logout", Logout);
        return group;
    }

    private static async Task<IResult> Register(RegisterRequest request, AuthService auth, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return MissingCredentials();
        }

        var result = await auth.RegisterAsync(request.Email.Trim(), request.Password, ct);
        return result.Ok
            ? Results.Json(ToResponse(result.Tokens!), statusCode: StatusCodes.Status201Created)
            : ApiProblems.Auth(result);
    }

    private static async Task<IResult> Login(LoginRequest request, AuthService auth, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return MissingCredentials();
        }

        var result = await auth.LoginAsync(request.Email.Trim(), request.Password, ct);
        return result.Ok ? Results.Ok(ToResponse(result.Tokens!)) : ApiProblems.Auth(result);
    }

    private static async Task<IResult> Refresh(RefreshRequest request, AuthService auth, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return ApiProblems.Auth(AuthResult.Fail(AuthFailure.InvalidCredentials));
        }

        var result = await auth.RefreshAsync(request.RefreshToken, ct);
        return result.Ok ? Results.Ok(ToResponse(result.Tokens!)) : ApiProblems.Auth(result);
    }

    private static async Task<IResult> Logout(LogoutRequest request, AuthService auth, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            await auth.RevokeAsync(request.RefreshToken, ct);
        }

        return Results.NoContent();
    }

    private static IResult MissingCredentials() => Results.Problem(
        title: "Email and password are required",
        statusCode: StatusCodes.Status400BadRequest);

    private static AuthResponse ToResponse(IssuedTokens tokens) => new(
        tokens.Access.Value,
        tokens.Access.ExpiresAt,
        tokens.RefreshToken,
        tokens.User.Id,
        tokens.User.Email ?? "");
}
