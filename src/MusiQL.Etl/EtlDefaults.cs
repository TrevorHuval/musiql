namespace MusiQL.Etl;

public static class EtlDefaults
{
    private const string DevConnection =
        "Host=localhost;Port=5442;Database=musiql;Username=musiql;Password=musiql";

    public static string CacheDirectory => Path.Combine(Environment.CurrentDirectory, ".mbdump-cache");

    public static string SampleDirectory => Path.Combine(AppContext.BaseDirectory, "sample", "mbdump");

    public static string ConnectionString(string? explicitValue) =>
        explicitValue
        ?? Environment.GetEnvironmentVariable("MUSIQL_CONNECTION")
        ?? DevConnection;

    public static IReadOnlyList<string> Tarballs(string directory)
    {
        var paths = new[] { "mbdump.tar.bz2", "mbdump-derived.tar.bz2" }
            .Select(name => Path.Combine(directory, name))
            .ToArray();

        var missing = paths.Where(p => !File.Exists(p)).ToArray();
        if (missing.Length > 0)
        {
            throw new FileNotFoundException(
                $"missing dump tarballs in {directory}: {string.Join(", ", missing.Select(Path.GetFileName))}. " +
                "Run 'etl download' first.");
        }

        return paths;
    }
}
