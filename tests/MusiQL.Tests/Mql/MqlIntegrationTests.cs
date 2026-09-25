using Microsoft.EntityFrameworkCore;
using MusiQL.Data.App;
using Xunit.Abstractions;

namespace MusiQL.Tests.Mql;

[Collection("mql-db")]
public class MqlIntegrationTests(SampleDatabaseFixture fixture, ITestOutputHelper output)
{
    private static readonly Guid UserA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task Canonical_grunge_query_returns_the_expected_artists()
    {
        if (Unavailable())
        {
            return;
        }

        var result = await fixture.RunAsync(
            "tracks where genre = \"grunge\" and year between 1990 and 2004 and artist != \"Nirvana\"");

        var artists = result.Rows.Select(r => (string)r[2]!).Distinct().ToList();
        Assert.Contains("Pearl Jam", artists);
        Assert.Contains("Alice in Chains", artists);
        Assert.DoesNotContain("Nirvana", artists);
    }

    [Theory]
    [InlineData("tracks where genre = \"grunge\" order by year desc limit 500")]
    [InlineData("tracks where genre in (\"grunge\", \"hip hop\") limit 500")]
    [InlineData("albums where genre = \"grunge\" order by votes desc limit 500")]
    [InlineData("artists where genre = \"grunge\" limit 500")]
    [InlineData("tracks where genre != \"grunge\" and year = 1991 limit 500")]
    public async Task Selective_genre_shape_returns_the_same_rows(string mql)
    {
        if (Unavailable())
        {
            return;
        }

        var genres = new HashSet<string> { "grunge", "hip hop" };
        var probe = await fixture.RunAsync(mql, selectiveGenres: genres);
        var correlated = await fixture.RunAsync(mql);

        Assert.NotEmpty(correlated.Rows);
        Assert.Equal(
            correlated.Rows.Select(r => (Guid)r[0]!).Order(),
            probe.Rows.Select(r => (Guid)r[0]!).Order());
    }

    [Fact]
    public async Task Unordered_queries_rank_by_popularity_with_unknowns_last()
    {
        if (Unavailable())
        {
            return;
        }

        await using var connection = new Npgsql.NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using (var seed = new Npgsql.NpgsqlCommand("""
            DELETE FROM catalog.recording_popularity;
            INSERT INTO catalog.recording_popularity (recording_id, listeners, listens)
            SELECT r.id, CASE r.name WHEN 'Alive' THEN 900 ELSE 50 END, 0
            FROM catalog.recording r WHERE r.name IN ('Alive', 'Would?');
            """, connection))
        {
            await seed.ExecuteNonQueryAsync();
        }

        try
        {
            var result = await fixture.RunAsync("tracks where genre = \"grunge\" limit 500");
            var popularity = result.Rows.Select(r => (int)r[6]!).ToList();

            Assert.Equal("Alive", (string)result.Rows[0][1]!);
            Assert.Equal(900, popularity[0]);
            Assert.Equal(popularity.OrderByDescending(p => p), popularity);
        }
        finally
        {
            await using var clear = new Npgsql.NpgsqlCommand("DELETE FROM catalog.recording_popularity", connection);
            await clear.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Genre_and_year_filters_stay_within_bounds()
    {
        if (Unavailable())
        {
            return;
        }

        var result = await fixture.RunAsync("tracks where genre = \"grunge\" and year between 1990 and 2004");

        Assert.NotEmpty(result.Rows);
        foreach (var row in result.Rows)
        {
            var year = (short?)row[4];
            Assert.NotNull(year);
            Assert.InRange(year!.Value, (short)1990, (short)2004);
        }
    }

    [Fact]
    public async Task Limit_caps_the_row_count()
    {
        if (Unavailable())
        {
            return;
        }

        var result = await fixture.RunAsync("tracks limit 5");
        Assert.True(result.Rows.Count <= 5);
    }

    [Fact]
    public async Task From_library_returns_only_the_querying_users_rows()
    {
        if (Unavailable())
        {
            return;
        }

        Guid[] aMbids;
        Guid[] bMbids;
        await using (var db = fixture.CreateContext())
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM app.user_library");
            var recordings = db.Recordings.OrderBy(r => r.Id).Take(3).ToList();

            db.UserLibrary.Add(new UserLibrary { UserId = UserA, RecordingId = recordings[0].Id });
            db.UserLibrary.Add(new UserLibrary { UserId = UserA, RecordingId = recordings[1].Id });
            db.UserLibrary.Add(new UserLibrary { UserId = UserB, RecordingId = recordings[2].Id });
            await db.SaveChangesAsync();

            aMbids = [recordings[0].Mbid, recordings[1].Mbid];
            bMbids = [recordings[2].Mbid];
        }

        var aResult = await fixture.RunAsync("tracks from library", UserA);
        var bResult = await fixture.RunAsync("tracks from library", UserB);

        Assert.Equal(aMbids.ToHashSet(), aResult.Rows.Select(r => (Guid)r[0]!).ToHashSet());
        Assert.Equal(bMbids.ToHashSet(), bResult.Rows.Select(r => (Guid)r[0]!).ToHashSet());
        Assert.Empty(aResult.Rows.Select(r => (Guid)r[0]!).Intersect(bMbids));
    }

    private bool Unavailable()
    {
        if (fixture.Available)
        {
            return false;
        }

        output.WriteLine("Postgres not reachable on localhost:5442; skipping MQL integration test.");
        return true;
    }
}
