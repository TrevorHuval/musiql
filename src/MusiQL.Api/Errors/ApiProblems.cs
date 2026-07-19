using MusiQL.Api.Auth;
using MusiQL.Api.Contracts;
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
        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
    };

    public static IResult NotFound(string what) => Results.Problem(
        title: $"{what} not found",
        statusCode: StatusCodes.Status404NotFound,
        type: Base + "not-found");
}
