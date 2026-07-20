using System.Security.Claims;
using MusiQL.Api.Auth;
using MusiQL.Api.Contracts;
using MusiQL.Api.Errors;
using MusiQL.Api.Spotify;

namespace MusiQL.Api.Endpoints;

public static class SpotifyEndpoints
{
    public static RouteGroupBuilder MapSpotifyEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/status", Status);
        group.MapPost("/connect", Connect);
        group.MapPost("/callback", Callback);
        group.MapPost("/disconnect", Disconnect);
        group.MapPost("/library/sync", SyncLibrary).RequireRateLimiting(RateLimits.Query);
        return group;
    }

    private static async Task<IResult> Status(ClaimsPrincipal principal, SpotifyAuthService auth, CancellationToken ct)
    {
        var status = await auth.GetStatusAsync(principal.UserId()!.Value, ct);
        return Results.Ok(status);
    }

    private static async Task<IResult> Connect(ClaimsPrincipal principal, SpotifyAuthService auth, CancellationToken ct)
    {
        try
        {
            var url = await auth.BuildConnectUrlAsync(principal.UserId()!.Value, ct);
            return Results.Ok(new SpotifyConnectResponse(url));
        }
        catch (Exception ex) when (ex is SpotifyNotConfiguredException)
        {
            return ApiProblems.Spotify(ex);
        }
    }

    private static async Task<IResult> Callback(
        SpotifyCallbackRequest request, ClaimsPrincipal principal, SpotifyAuthService auth, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.State) || string.IsNullOrWhiteSpace(request.Code))
        {
            return Results.Problem(title: "state and code are required", statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var status = await auth.CompleteAsync(principal.UserId()!.Value, request.State, request.Code, ct);
            return Results.Ok(status);
        }
        catch (Exception ex) when (ex is SpotifyNotConfiguredException or SpotifyAuthStateException or SpotifyApiException)
        {
            return ApiProblems.Spotify(ex);
        }
    }

    private static async Task<IResult> Disconnect(
        ClaimsPrincipal principal, SpotifyAuthService auth, CancellationToken ct)
    {
        await auth.DisconnectAsync(principal.UserId()!.Value, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> SyncLibrary(
        ClaimsPrincipal principal, SpotifyLibraryService library, CancellationToken ct)
    {
        try
        {
            var result = await library.SyncAsync(principal.UserId()!.Value, ct);
            return Results.Ok(result);
        }
        catch (Exception ex) when (ex is SpotifyNotConfiguredException or SpotifyNotConnectedException or SpotifyApiException)
        {
            return ApiProblems.Spotify(ex);
        }
    }
}
