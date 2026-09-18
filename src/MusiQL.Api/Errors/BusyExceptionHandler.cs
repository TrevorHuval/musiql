using Microsoft.AspNetCore.Diagnostics;
using MusiQL.Api.Query;

namespace MusiQL.Api.Errors;

public sealed class BusyExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken ct)
    {
        switch (exception)
        {
            case QueryBusyException busy:
                context.Response.Headers.RetryAfter = Math.Ceiling(busy.RetryAfter.TotalSeconds)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
                await ApiProblems.Busy(busy.Message).ExecuteAsync(context);
                return true;
            case OperationInProgressException inProgress:
                await ApiProblems.InProgress(inProgress.Message).ExecuteAsync(context);
                return true;
            default:
                return false;
        }
    }
}
