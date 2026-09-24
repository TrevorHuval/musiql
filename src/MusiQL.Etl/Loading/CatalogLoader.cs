using System.Globalization;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MusiQL.Data;
using MusiQL.Etl.Tsv;
using Npgsql;
using NpgsqlTypes;

namespace MusiQL.Etl.Loading;

// minGenreVotes > 0 trims the catalog to artists with at least that many genre
// votes; see Sql/trim.sql.
public sealed class CatalogLoader(string connectionString, Action<string> log, int minGenreVotes = 0)
{
    private static readonly IReadOnlyDictionary<string, TableSpec> Specs =
        TableSpecs.All.ToDictionary(s => s.Table);

    // Session-level advisory lock so two loads cannot run against one database.
    private const long LoadLockKey = 0x4d7573694c6f6164;

    // The swap TRUNCATEs the catalog, which needs an exclusive lock on every
    // table. Rather than queue behind a long-running reader (and block every
    // reader arriving after us), give up and let the operator retry.
    private static readonly TimeSpan SwapLockTimeout = TimeSpan.FromSeconds(30);

    public void Load(IDumpSource source)
    {
        log("applying migrations");
        using (var context = MusiQLDbContextFactory.Create(connectionString))
        {
            context.Database.Migrate();
        }

        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();

        if (!TryLock(connection))
        {
            throw new InvalidOperationException("Another catalog load is already running against this database.");
        }

        try
        {
            log("resetting staging schema");
            Execute(connection, Script("staging.sql"));

            var wanted = Specs.Keys.ToHashSet();
            foreach (var table in source.Read(wanted))
            {
                var rows = Copy(connection, Specs[table.Table], table.Reader);
                log($"staging.{table.Table}: {rows:N0} rows");
            }

            log("indexing staging");
            Execute(connection, Script("index.sql"));

            if (minGenreVotes > 0)
            {
                log($"trimming to artists with at least {minGenreVotes} genre votes");
                Execute(connection, Script("trim.sql").Replace(
                    "{min_votes}", minGenreVotes.ToString(CultureInfo.InvariantCulture)));
            }

            log("transforming to catalog shape");
            Execute(connection, Script("transform.sql"));

            log("swapping catalog (readers block until this commits)");
            using (var swap = connection.BeginTransaction())
            {
                Execute(connection, $"SET LOCAL lock_timeout = {(int)SwapLockTimeout.TotalMilliseconds};", swap);
                Execute(connection, Script("swap.sql"), swap);
                swap.Commit();
            }

            log("analyzing catalog");
            Execute(connection, "ANALYZE catalog.artist, catalog.genre, catalog.release_group, catalog.release, "
                + "catalog.recording, catalog.artist_genre, catalog.release_group_genre, catalog.recording_genre;");
        }
        finally
        {
            try
            {
                Execute(connection, "DROP SCHEMA IF EXISTS staging CASCADE;");
                Execute(connection, $"SELECT pg_advisory_unlock({LoadLockKey});");
            }
            catch (NpgsqlException ex)
            {
                log($"cleanup failed, staging schema may remain: {ex.Message}");
            }
        }

        log("done");
    }

    private static bool TryLock(NpgsqlConnection connection)
    {
        using var command = new NpgsqlCommand($"SELECT pg_try_advisory_lock({LoadLockKey});", connection);
        return (bool)command.ExecuteScalar()!;
    }

    private static long Copy(NpgsqlConnection connection, TableSpec spec, TextReader reader)
    {
        using var importer = connection.BeginBinaryImport(
            $"COPY staging.{spec.Table} ({spec.ColumnList}) FROM STDIN (FORMAT BINARY)");

        long rows = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var fields = Mbdump.SplitFields(line);
            importer.StartRow();
            foreach (var column in spec.Columns)
            {
                var value = Mbdump.Field(fields, column.Source);
                if (value is null)
                {
                    importer.WriteNull();
                    continue;
                }

                switch (column.Type)
                {
                    case FieldType.Int:
                        importer.Write(int.Parse(value, CultureInfo.InvariantCulture), NpgsqlDbType.Integer);
                        break;
                    case FieldType.Short:
                        importer.Write(short.Parse(value, CultureInfo.InvariantCulture), NpgsqlDbType.Smallint);
                        break;
                    case FieldType.Uuid:
                        importer.Write(Guid.Parse(value), NpgsqlDbType.Uuid);
                        break;
                    case FieldType.Text:
                        importer.Write(value, NpgsqlDbType.Text);
                        break;
                }
            }

            rows++;
        }

        importer.Complete();
        return rows;
    }

    private static void Execute(NpgsqlConnection connection, string sql, NpgsqlTransaction? transaction = null)
    {
        using var command = new NpgsqlCommand(sql, connection, transaction) { CommandTimeout = 0 };
        command.ExecuteNonQuery();
    }

    private static string Script(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resource = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith($".Sql.{name}", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
