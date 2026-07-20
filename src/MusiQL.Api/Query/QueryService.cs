using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MusiQL.Api.Contracts;
using MusiQL.Core.Mql;
using MusiQL.Core.Mql.Compilation;
using MusiQL.Core.Mql.Execution;
using MusiQL.Data;

namespace MusiQL.Api.Query;

public sealed class QueryService(MqlEngine engine, MusiQLDbContext catalog, IOptions<QueryOptions> options)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;

    public MqlCompilation Compile(string mql, Guid? callerUserId) =>
        engine.Compile(mql, new CompileContext { CallerUserId = callerUserId });

    public Task<QueryResult> ExecuteAsync(CompiledQuery query, CancellationToken ct)
    {
        var executor = new QueryExecutor(new ExecutionOptions(options.Value.ConnectionString)
        {
            StatementTimeout = options.Value.StatementTimeout
        });
        return executor.ExecuteAsync(query, ct);
    }

    public async Task<QueryPageResponse> RunAsync(
        CompiledQuery query, Guid? callerUserId, int page, int pageSize, CancellationToken ct)
    {
        var result = await ExecuteAsync(query, ct);
        var hint = await LibraryHint(query, callerUserId, result.Rows.Count, ct);
        return Paginate(query.Entity, Columns(result), result.Rows, page, pageSize, hint);
    }

    public static IReadOnlyList<QueryColumn> Columns(QueryResult result) =>
        result.Columns.Select(c => new QueryColumn(c.Name, ColumnTypes.Of(c.ClrType))).ToList();

    public static QueryPageResponse Paginate(
        string entity,
        IReadOnlyList<QueryColumn> columns,
        IReadOnlyList<IReadOnlyList<object?>> rows,
        int page,
        int pageSize,
        string? hint)
    {
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var total = rows.Count;
        var slice = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new QueryPageResponse(entity, columns, slice, page, pageSize, total, page * pageSize < total, hint);
    }

    private async Task<string?> LibraryHint(CompiledQuery query, Guid? callerUserId, int total, CancellationToken ct)
    {
        if (!query.FromLibrary || total > 0 || callerUserId is null)
        {
            return null;
        }

        var hasLibrary = await catalog.UserLibrary.AnyAsync(x => x.UserId == callerUserId, ct);
        return hasLibrary ? null : "library_empty";
    }
}
