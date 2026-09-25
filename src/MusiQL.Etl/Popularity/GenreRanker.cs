using System.Reflection;
using Npgsql;

namespace MusiQL.Etl.Popularity;

// Rebuilds catalog.genre_top_recording from the popularity and genre tables.
// Run after fetching popularity, after a catalog load, or after genre rules
// change; it replaces the table's contents in one transaction.
public sealed class GenreRanker(string connectionString, Action<string> log)
{
    public async Task RankAsync(CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        log("ranking each genre's most popular tracks");
        await using (var transaction = await connection.BeginTransactionAsync(ct))
        {
            await using var command = new NpgsqlCommand(Script("rank.sql"), connection, transaction) { CommandTimeout = 0 };
            await command.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
        }

        await using (var analyze = new NpgsqlCommand("VACUUM (ANALYZE) catalog.genre_top_recording", connection) { CommandTimeout = 0 })
        {
            await analyze.ExecuteNonQueryAsync(ct);
        }

        await using var count = new NpgsqlCommand("SELECT count(*) FROM catalog.genre_top_recording", connection);
        log($"ranked {(long)(await count.ExecuteScalarAsync(ct))!:N0} genre memberships");
    }

    private static string Script(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith($".Sql.{name}", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
