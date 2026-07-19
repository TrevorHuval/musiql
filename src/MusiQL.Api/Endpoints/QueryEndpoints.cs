using System.Security.Claims;
using MusiQL.Api.Auth;
using MusiQL.Api.Contracts;
using MusiQL.Api.Errors;
using MusiQL.Api.Query;

namespace MusiQL.Api.Endpoints;

public static class QueryEndpoints
{
    public static RouteGroupBuilder MapQueryEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/preview", Preview).RequireRateLimiting(RateLimits.Query);
        return group;
    }

    private static async Task<IResult> Preview(
        PreviewRequest request, ClaimsPrincipal principal, QueryService query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Mql))
        {
            return Results.Problem(title: "A query is required", statusCode: StatusCodes.Status400BadRequest);
        }

        var userId = principal.UserId();
        var compilation = query.Compile(request.Mql, userId);
        if (!compilation.Success)
        {
            return ApiProblems.MqlValidation(compilation.Errors);
        }

        var result = await query.RunAsync(
            compilation.Query!, userId, request.Page ?? 1, request.PageSize ?? QueryService.DefaultPageSize, ct);
        return Results.Ok(result);
    }
}
