using MusiQL.Etl;
using MusiQL.Etl.Download;
using MusiQL.Etl.Loading;

var command = args.Length > 0 ? args[0] : "help";
var options = CliOptions.Parse(args);

switch (command)
{
    case "download":
        await Download(options);
        return 0;
    case "load":
        Load(options);
        return 0;
    case "migrate":
        Migrate(options);
        return 0;
    default:
        Console.WriteLine(
            """
            musiql-etl — MusicBrainz catalog loader

            Commands:
              download [--out <dir>] [--base-url <url>]
                  Fetch the latest mbdump + mbdump-derived tarballs (only the files we load).

              load [--sample] [--source <dir>] [--connection <cs>] [--min-genre-votes <n>]
                  Apply migrations, stream-load the dump into staging, transform, and swap
                  into the catalog schema. --sample loads the checked-in dev fixture.
                  --min-genre-votes keeps only artists with at least n genre votes (on the
                  artist or its albums), for hosts that cannot hold the full catalog.

              migrate [--connection <cs>]
                  Apply catalog schema migrations only, leaving loaded data in place.

            Connection resolves from --connection, then the MUSIQL_CONNECTION environment
            variable, then the local dev database on port 5442.
            """);
        return command == "help" ? 0 : 1;
}

async Task Download(CliOptions o)
{
    var outDir = o.Value("out") ?? EtlDefaults.CacheDirectory;
    var baseUrl = o.Value("base-url") ?? DumpDownloader.DefaultBaseUrl;
    var downloader = new DumpDownloader(baseUrl, outDir, EtlLog.Write);
    await downloader.DownloadAsync();
}

void Migrate(CliOptions o)
{
    using var context = MusiQL.Data.MusiQLDbContextFactory.Create(EtlDefaults.ConnectionString(o.Value("connection")));
    EtlLog.Write("applying catalog migrations");
    Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.Migrate(context.Database);
    EtlLog.Write("done");
}

void Load(CliOptions o)
{
    var connection = EtlDefaults.ConnectionString(o.Value("connection"));
    var minVotes = int.TryParse(o.Value("min-genre-votes"), out var parsed) ? parsed : 0;
    var loader = new CatalogLoader(connection, EtlLog.Write, minVotes);

    IDumpSource source;
    if (o.Has("sample"))
    {
        var dir = o.Value("source") ?? EtlDefaults.SampleDirectory;
        EtlLog.Write($"loading sample fixture from {dir}");
        source = new DirectoryDumpSource(dir);
    }
    else
    {
        var dir = o.Value("source") ?? EtlDefaults.CacheDirectory;
        var tarballs = EtlDefaults.Tarballs(dir);
        EtlLog.Write($"loading dump tarballs from {dir}");
        source = new TarDumpSource(tarballs);
    }

    loader.Load(source);
}
