using MusiQL.Core.Mql.Compilation;
using MusiQL.Core.Mql.Execution;
using MusiQL.Core.Mql.Schema;
using Npgsql;
using Xunit.Abstractions;

namespace MusiQL.Tests.Mql;

public class ExecutionGuardTests(SampleDatabaseFixture fixture, ITestOutputHelper output)
    : IClassFixture<SampleDatabaseFixture>
{
    [Fact]
    public async Task Read_only_transaction_blocks_writes()
    {
        if (Unavailable())
        {
            return;
        }

        var write = new CompiledQuery(
            "INSERT INTO catalog.genre (id, mbid, name) VALUES (999999, gen_random_uuid(), 'intrusion')",
            [], "tracks", [], 500);

        var executor = new QueryExecutor(new ExecutionOptions(fixture.ConnectionString));
        var ex = await Assert.ThrowsAsync<PostgresException>(() => executor.ExecuteAsync(write));
        Assert.Equal(PostgresErrorCodes.ReadOnlySqlTransaction, ex.SqlState);
    }

    [Fact]
    public async Task Statement_timeout_aborts_a_slow_query()
    {
        if (Unavailable())
        {
            return;
        }

        var slow = new CompiledQuery(
            "SELECT pg_sleep(5)", [], "tracks", [new ResultColumn("sleep", typeof(object))], 500);

        var executor = new QueryExecutor(new ExecutionOptions(fixture.ConnectionString)
        {
            StatementTimeout = TimeSpan.FromMilliseconds(250)
        });

        var ex = await Assert.ThrowsAsync<PostgresException>(() => executor.ExecuteAsync(slow));
        Assert.Equal(PostgresErrorCodes.QueryCanceled, ex.SqlState);
    }

    private bool Unavailable()
    {
        if (fixture.Available)
        {
            return false;
        }

        output.WriteLine("Postgres not reachable on localhost:5442; skipping execution guard test.");
        return true;
    }
}
