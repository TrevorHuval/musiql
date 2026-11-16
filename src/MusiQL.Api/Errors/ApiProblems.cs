using MusiQL.Api.Auth;
using MusiQL.Api.Contracts;
using MusiQL.Api.Spotify;
using MusiQL.Core.Mql.Diagnostics;

namespace MusiQL.Api.Errors;

public static class ApiProblems
{
    private const string Base = "https://musiql.dev/problems/";

    public static IResult MqlValidation(IReadOnlyList<MqlError> errors)
    {
        var payload = errors
            .Select(e => new MqlErrorDto(e.Code.ToString(), e.Message, e.Span.Start, e.Span.Length, e.Expected))
            .ToArray();

        return Results.Problem(
            title: "The query could not be parsed",
            statusCode: StatusCodes.Status422UnprocessableEntity,
            type: Base + "mql-validation",
            extensions: new Dictionary<string, object?> { ["errors"] = payload });
    }

    public static IResult Auth(AuthResult result) => result.Failure switch
    {
        AuthFailure.Validation => Results.Problem(
            title: "Registration failed",
            statusCode: StatusCodes.Status400BadRequest,
            type: Base + "registration",
            extensions: new Dictionary<string, object?> { ["errors"] = result.Messages }),
        AuthFailure.InvalidCredentials => Results.Problem(
            title: "Invalid email or password",
            statusCode: StatusCodes.Status401Unauthorized,
            type: Base + "invalid-credentials"),
        AuthFailure.LockedOut => Results.Problem(
            title: "Account temporarily locked",
            detail: "Too many failed attempts. Try again later.",
            statusCode: StatusCodes.Status423Locked,
            type: Base + "account-locked"),
        AuthFailure.RegistrationClosed => Results.Problem(
            title: "Registration is closed",
            statusCode: StatusCodes.Status403Forbidden,
            type: Base + "registration-closed"),
        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
    };

    public static IResult NotFound(string what) => Results.Problem(
        title: $"{what} not found",
        statusCode: StatusCodes.Status404NotFound,
        type: Base + "not-found");

    public static IResult Invalid(string title, string? detail = null) => Results.Problem(
        title: title,
        detail: detail,
        statusCode: StatusCodes.Status400BadRequest,
        type: Base + "validation");

    public static IResult QuotaExceeded(string detail) => Results.Problem(
        title: "Limit reached",
        detail: detail,
        statusCode: StatusCodes.Status403Forbidden,
        type: Base + "quota");

    public static IResult QueryTooSlow() => Results.Problem(
        title: "Query took too long",
        detail: "Narrow it with a genre, artist, or year range and try again.",
        statusCode: StatusCodes.Status422UnprocessableEntity,
        type: Base + "query-timeout");

    public static IResult Busy(string detail) => Results.Problem(
        title: "Server busy",
        detail: detail,
        statusCode: StatusCodes.Status503ServiceUnavailable,
        type: Base + "busy");

    public static IResult InProgress(string detail) => Results.Problem(
        title: "Operation already running",
        detail: detail,
        statusCode: StatusCodes.Status409Conflict,
        type: Base + "in-progress");

    public static IResult ExportUnsupported(string detail) => Results.Problem(
        title: "Playlist cannot be exported",
        detail: detail,
        statusCode: StatusCodes.Status422UnprocessableEntity,
        type: Base + "export-unsupported");

    public static IResult Spotify(Exception exception) => exception switch
    {
        SpotifyNotConfiguredException => Results.Problem(
            title: "Spotify integration is not configured",
            detail: "The server is missing the Spotify client id or token encryption key.",
            statusCode: StatusCodes.Status503ServiceUnavailable,
            type: Base + "spotify-unconfigured"),
        SpotifyNotConnectedException => Results.Problem(
            title: "Spotify account not connected",
            detail: "Connect a Spotify account before running this action.",
            statusCode: StatusCodes.Status409Conflict,
            type: Base + "spotify-not-connected"),
        SpotifyAuthStateException => Results.Problem(
            title: "Invalid Spotify authorization",
            detail: "The authorization request expired or did not match. Start the connection again.",
            statusCode: StatusCodes.Status400BadRequest,
            type: Base + "spotify-auth-state"),
        ExportNotSupportedException e => Results.Problem(
            title: "Playlist cannot be exported",
            detail: e.Message,
            statusCode: StatusCodes.Status422UnprocessableEntity,
            type: Base + "export-unsupported"),
        SpotifyApiException e => Results.Problem(
            title: "Spotify request failed",
            detail: e.Message,
            statusCode: StatusCodes.Status502BadGateway,
            type: Base + "spotify-upstream"),
        SpotifyAccessRestrictedException => Results.Problem(
            title: "Spotify access is invite-only",
            detail: "MusiQL's Spotify connection is invite-only for now and this Spotify account "
                + "hasn't been added yet. Request access from Settings.",
            statusCode: StatusCodes.Status403Forbidden,
            type: Base + "spotify-invite-only"),
        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
    };

    public static IResult AccessRequest(Exception exception) => exception switch
    {
        AccessRequestsDisabledException => Results.Problem(
            title: "Access requests are not available",
            detail: "Requesting Spotify access isn't set up on this server.",
            statusCode: StatusCodes.Status503ServiceUnavailable,
            type: Base + "access-request-disabled"),
        AccessRequestValidationException e => Results.Problem(
            title: "Check your request",
            detail: e.Message,
            statusCode: StatusCodes.Status400BadRequest,
            type: Base + "access-request-invalid"),
        AccessRequestCooldownException e => Results.Problem(
            title: "Already requested",
            detail: "You already asked for access. You can ask again after the cooldown.",
            statusCode: StatusCodes.Status429TooManyRequests,
            type: Base + "access-request-cooldown",
            extensions: new Dictionary<string, object?> { ["retryAt"] = e.RetryAt }),
        AccessRequestLimitException => Results.Problem(
            title: "Try again tomorrow",
            detail: "MusiQL has received a lot of access requests today. Please try again tomorrow.",
            statusCode: StatusCodes.Status429TooManyRequests,
            type: Base + "access-request-limit"),
        AccessRequestDeliveryException => Results.Problem(
            title: "Request not sent",
            detail: "The request couldn't be delivered. Please try again later.",
            statusCode: StatusCodes.Status502BadGateway,
            type: Base + "access-request-delivery"),
        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
    };
}
