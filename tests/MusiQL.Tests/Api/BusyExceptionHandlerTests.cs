using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MusiQL.Api.Errors;
using Npgsql;

namespace MusiQL.Tests.Api;

public class BusyExceptionHandlerTests
{
    [Fact]
    public async Task Statement_timeout_becomes_a_422()
    {
        var context = NewContext();
        var timeout = new PostgresException(
            "canceling statement due to statement timeout", "ERROR", "ERROR", PostgresErrorCodes.QueryCanceled);

        var handled = await new BusyExceptionHandler().TryHandleAsync(context, timeout, default);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, context.Response.StatusCode);
    }

    [Fact]
    public async Task Other_database_errors_are_left_alone()
    {
        var context = NewContext();
        var other = new PostgresException("boom", "ERROR", "ERROR", PostgresErrorCodes.UndefinedTable);

        Assert.False(await new BusyExceptionHandler().TryHandleAsync(context, other, default));
    }

    private static DefaultHttpContext NewContext() => new()
    {
        RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider(),
        Response = { Body = new MemoryStream() }
    };
}
