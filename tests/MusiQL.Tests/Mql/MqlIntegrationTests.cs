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
