using System.Runtime.CompilerServices;
using Npgsql;
using Xunit.Abstractions;

namespace MusiQL.Tests.Api;

[Collection("api")]
public class QueryRoleTests(ApiFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public async Task Query_role_reads_catalog_but_not_identity_and_cannot_write()
    {
        if (Skip())
        {
            return;
        }

        await ApplyRoleScriptAsync();

        var builder = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Username = "musiql_query",
            Password = "change-me"
        };

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        await using (var catalogRead = new NpgsqlCommand("SELECT count(*) FROM catalog.artist", connection))
        {
            Assert.True(Convert.ToInt64(await catalogRead.ExecuteScalarAsync()) > 0);
        }

        await using (var libraryRead = new NpgsqlCommand("SELECT count(*) FROM app.user_library", connection))
        {
            Assert.Equal(0L, Convert.ToInt64(await libraryRead.ExecuteScalarAsync()));
        }

        await using (var identity = new NpgsqlCommand("SELECT count(*) FROM app.users", connection))
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(() => identity.ExecuteScalarAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
        }

        await using (var write = new NpgsqlCommand(
            "INSERT INTO catalog.genre (id, mbid, name) VALUES (999998, gen_random_uuid(), 'x')", connection))
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(() => write.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
        }
    }

    private async Task ApplyRoleScriptAsync()
    {
        var script = await File.ReadAllTextAsync(RoleScriptPath());
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(script, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string RoleScriptPath([CallerFilePath] string path = "")
    {
        var testDir = Path.GetDirectoryName(path)!;
        return Path.GetFullPath(Path.Combine(
            testDir, "..", "..", "..", "src", "MusiQL.Data", "Sql", "query_role.sql"));
    }

    private bool Skip()
    {
        if (fixture.Available)
        {
            return false;
        }

        output.WriteLine("Postgres not reachable on localhost:5442; skipping query role test.");
        return true;
    }
}
