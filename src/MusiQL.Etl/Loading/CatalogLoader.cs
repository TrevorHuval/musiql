using System.Globalization;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MusiQL.Data;
using MusiQL.Etl.Tsv;
using Npgsql;
using NpgsqlTypes;

namespace MusiQL.Etl.Loading;

public sealed class CatalogLoader(string connectionString, Action<string> log)
{
    private static readonly IReadOnlyDictionary<string, TableSpec> Specs =
        TableSpecs.All.ToDictionary(s => s.Table);

    public void Load(IDumpSource source)
    {
        log("applying migrations");
        using (var context = MusiQLDbContextFactory.Create(connectionString))
        {
            context.Database.Migrate();
        }

        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();

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

        log("transforming to catalog shape");
        Execute(connection, Script("transform.sql"));

        log("swapping catalog");
        using (var swap = connection.BeginTransaction())
        {
            Execute(connection, Script("swap.sql"), swap);
            swap.Commit();
        }

        Execute(connection, "DROP SCHEMA staging CASCADE;");
        log("done");
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
