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
    case "popularity":
        await Popularity(options);
        return 0;
    case "deezer":
    {
        var connection = EtlDefaults.ConnectionString(options.Value("connection"));
        var artists = int.TryParse(options.Value("artists"), out var n) ? n : 40000;
        using var cancel = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
        await new MusiQL.Etl.Popularity.DeezerFetcher(connection, EtlLog.Write)
            .FetchAsync(artists, options.Has("restart"), options.Value("base-url") ?? MusiQL.Etl.Popularity.DeezerFetcher.DefaultBaseUrl, cancel.Token);
        await new MusiQL.Etl.Popularity.GenreRanker(connection, EtlLog.Write).RankAsync(cancel.Token);
        return 0;
    }
    case "rank":
        await new MusiQL.Etl.Popularity.GenreRanker(
            EtlDefaults.ConnectionString(options.Value("connection")), EtlLog.Write).RankAsync(default);
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

              popularity [--only artist,release_group,recording] [--restart] [--connection <cs>]
                  Fetch ListenBrainz listener counts into catalog.*_popularity. Resumable:
                  a rerun continues after the last stored id unless --restart is given.
                  Ends by rebuilding the per-genre popularity ranking.

              deezer [--artists 40000] [--restart] [--connection <cs>]
                  Fetch current Deezer rank for the top tracks of the most popular artists
                  (ListenBrainz order) and blend it into the popularity score. Resumable.

              rank [--connection <cs>]
                  Recompute the blended popularity score and rebuild each genre's ranked
                  top tracks (catalog.genre_top_recording).

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

async Task Popularity(CliOptions o)
{
    var connection = EtlDefaults.ConnectionString(o.Value("connection"));
    var only = o.Value("only")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    using var cancel = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
    var fetcher = new MusiQL.Etl.Popularity.PopularityFetcher(connection, EtlLog.Write);
    await fetcher.FetchAsync(only, o.Has("restart"), o.Value("base-url") ?? MusiQL.Etl.Popularity.PopularityFetcher.DefaultBaseUrl, cancel.Token);
    await new MusiQL.Etl.Popularity.GenreRanker(connection, EtlLog.Write).RankAsync(cancel.Token);
}

void Migrate(CliOptions o)
{
    using var context = MusiQL.Data.MusiQLDbContextFactory.Create(EtlDefaults.ConnectionString(o.Value("connection")));
    EtlLog.Write("applying catalog migrations");
    // Index migrations on a full catalog take minutes; no command timeout.
    Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.SetCommandTimeout(context.Database, TimeSpan.Zero);
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
