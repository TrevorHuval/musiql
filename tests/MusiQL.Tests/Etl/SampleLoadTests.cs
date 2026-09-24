using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using MusiQL.Data;
using MusiQL.Etl.Loading;
using Npgsql;
using Xunit.Abstractions;

namespace MusiQL.Tests.Etl;

public class SampleLoadTests(ITestOutputHelper output)
{
    private const string Server = "Host=localhost;Port=5442;Username=musiql;Password=musiql";
    private const string TestConnection = $"{Server};Database=musiql_etl_test";

    [Fact]
    public void Sample_load_populates_catalog_and_applies_filter_rules()
    {
        if (!ServerReachable())
        {
            output.WriteLine("Postgres not reachable on localhost:5442; skipping sample load integration test.");
            return;
        }

        new CatalogLoader(TestConnection, output.WriteLine)
            .Load(new DirectoryDumpSource(SampleDirectory()));

        using var db = MusiQLDbContextFactory.Create(TestConnection);

        Assert.Equal(47, db.Artists.Count());
        Assert.Equal(119, db.Recordings.Count());
        Assert.False(db.Artists.Any(a => a.Name == "Sketchpad Sessions"),
            "artists with no releases must be skipped");
        Assert.False(db.ReleaseGroups.Any(rg => rg.Name == "Outcesticide"),
            "release groups with no official release must be skipped");

        var alive = db.Recordings.Where(r => r.Name == "Alive").ToList();
        Assert.Single(alive);
        Assert.Equal((short)1991, alive[0].FirstReleaseYear);

        var grunge =
            from r in db.Recordings
            join a in db.Artists on r.ArtistId equals a.Id
            join rg in db.RecordingGenres on r.Id equals rg.RecordingId
            join g in db.Genres on rg.GenreId equals g.Id
            where g.Name == "grunge"
                  && r.FirstReleaseYear >= 1990 && r.FirstReleaseYear <= 2004
                  && a.Name != "Nirvana"
            select a.Name;

        var artists = grunge.Distinct().ToList();
        Assert.Contains("Pearl Jam", artists);
        Assert.Contains("Alice in Chains", artists);
        Assert.DoesNotContain("Nirvana", artists);

        var nirvanaInRange = db.Recordings
            .Where(r => r.Artist!.Name == "Nirvana"
                        && r.FirstReleaseYear >= 1990 && r.FirstReleaseYear <= 2004)
            .Join(db.RecordingGenres, r => r.Id, rg => rg.RecordingId, (_, rg) => rg)
            .Join(db.Genres, rg => rg.GenreId, g => g.Id, (_, g) => g.Name)
            .Count(name => name == "grunge");
        Assert.Equal(5, nirvanaInRange);
    }

    [Fact]
    public void Genre_vote_trim_drops_weakly_tagged_artists_and_their_tracks()
    {
        if (!ServerReachable())
        {
            output.WriteLine("Postgres not reachable on localhost:5442; skipping trim integration test.");
            return;
        }

        const string trimConnection = $"{Server};Database=musiql_etl_trim_test";
        new CatalogLoader(trimConnection, output.WriteLine, minGenreVotes: 36)
            .Load(new DirectoryDumpSource(SampleDirectory()));

        using var db = MusiQLDbContextFactory.Create(trimConnection);
        var artists = db.Artists.Select(a => a.Name).ToList();
        output.WriteLine($"{artists.Count} artists, {db.Recordings.Count()} recordings after trim");

        Assert.Contains("Pearl Jam", artists);
        Assert.DoesNotContain("The Smashing Pumpkins", artists);
        Assert.InRange(artists.Count, 1, 46);
        Assert.False(db.Recordings.Any(r => r.Artist!.Name == "The Smashing Pumpkins"));
    }

    private static bool ServerReachable()
    {
        try
        {
            using var connection = new NpgsqlConnection($"{Server};Database=postgres;Timeout=3");
            connection.Open();
            return true;
        }
        catch (NpgsqlException)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static string SampleDirectory([CallerFilePath] string path = "")
    {
        var testDir = Path.GetDirectoryName(path)!;
        return Path.GetFullPath(
            Path.Combine(testDir, "..", "..", "..", "src", "MusiQL.Etl", "sample", "mbdump"));
    }
}
