using MusiQL.Core.Mql.Compilation;
using Npgsql;

namespace MusiQL.Core.Mql.Execution;

public sealed class QueryExecutor(ExecutionOptions options)
{
    public async Task<QueryResult> ExecuteAsync(CompiledQuery query, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var timeout = (int)options.StatementTimeout.TotalMilliseconds;
        await using (var guard = new NpgsqlCommand(
            $"SET LOCAL statement_timeout = {timeout}; SET TRANSACTION READ ONLY;", connection, transaction))
        {
            await guard.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = new NpgsqlCommand(query.Sql, connection, transaction);
        foreach (var parameter in query.Parameters)
        {
            command.Parameters.Add(new NpgsqlParameter(parameter.Name, parameter.Value));
        }

        var columnCount = query.ResultColumns.Count;
        var rows = new List<IReadOnlyList<object?>>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (rows.Count < query.RowLimit && await reader.ReadAsync(cancellationToken))
            {
                var row = new object?[columnCount];
                for (var i = 0; i < columnCount; i++)
                {
                    row[i] = await reader.IsDBNullAsync(i, cancellationToken)
                        ? null
                        : reader.GetValue(i);
                }

                rows.Add(row);
            }
        }

        await transaction.RollbackAsync(cancellationToken);
        return new QueryResult(query.ResultColumns, rows);
    }
}
