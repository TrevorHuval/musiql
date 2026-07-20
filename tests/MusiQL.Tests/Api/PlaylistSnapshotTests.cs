using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MusiQL.Api.Contracts;
using Xunit.Abstractions;

namespace MusiQL.Tests.Api;

[Collection("api")]
public class PlaylistSnapshotTests(ApiFixture fixture, ITestOutputHelper output)
{
    private const string GrungeMql =
        "tracks where genre = \"grunge\" and year between 1990 and 2004 and artist != \"Nirvana\"";

    [Fact]
    public async Task Reading_tracks_caches_a_snapshot_and_serves_it()
    {
        if (Skip())
        {
            return;
        }

        var client = await fixture.RegisterClientAsync();
        var created = await client.PostAsJsonAsync("/api/playlists", new PlaylistRequest("Grunge", null, GrungeMql));
        var playlist = (await created.Content.ReadFromJsonAsync<PlaylistResponse>())!;

        var first = await client.GetFromJsonAsync<QueryPageResponse>($"/api/playlists/{playlist.Id}/tracks");
        Assert.NotEmpty(first!.Rows);

        await using (var db = fixture.CreateAppContext())
        {
            var snapshot = await db.PlaylistSnapshots.FirstOrDefaultAsync(s => s.PlaylistId == playlist.Id);
            Assert.NotNull(snapshot);
            Assert.Equal(GrungeMql, snapshot!.MqlText);

            snapshot.Payload = SentinelPayload();
            await db.SaveChangesAsync();
        }

        var second = await client.GetFromJsonAsync<QueryPageResponse>($"/api/playlists/{playlist.Id}/tracks");
        Assert.Single(second!.Rows);
        Assert.Equal("Sentinel Track", Cell(second, "title"));
    }

    [Fact]
    public async Task Editing_the_definition_invalidates_the_snapshot()
    {
        if (Skip())
        {
            return;
        }

        var client = await fixture.RegisterClientAsync();
        var created = await client.PostAsJsonAsync("/api/playlists", new PlaylistRequest("Grunge", null, GrungeMql));
        var playlist = (await created.Content.ReadFromJsonAsync<PlaylistResponse>())!;

        await client.GetFromJsonAsync<QueryPageResponse>($"/api/playlists/{playlist.Id}/tracks");

        await using (var db = fixture.CreateAppContext())
        {
            var snapshot = await db.PlaylistSnapshots.FirstAsync(s => s.PlaylistId == playlist.Id);
            snapshot.Payload = SentinelPayload();
            await db.SaveChangesAsync();
        }

        await client.PutAsJsonAsync(
            $"/api/playlists/{playlist.Id}", new PlaylistRequest("Grunge", null, "tracks where genre = \"grunge\""));

        var page = await client.GetFromJsonAsync<QueryPageResponse>($"/api/playlists/{playlist.Id}/tracks");
        Assert.NotEqual("Sentinel Track", Cell(page!, "title"));
        Assert.True(page!.Rows.Count > 1);
    }

    [Fact]
    public async Task Library_scoped_playlists_are_not_snapshotted()
    {
        if (Skip())
        {
            return;
        }

        var client = await fixture.RegisterClientAsync();
        var created = await client.PostAsJsonAsync(
            "/api/playlists", new PlaylistRequest("Mine", null, "tracks from library"));
        var playlist = (await created.Content.ReadFromJsonAsync<PlaylistResponse>())!;

        await client.GetFromJsonAsync<QueryPageResponse>($"/api/playlists/{playlist.Id}/tracks");

        await using var db = fixture.CreateAppContext();
        Assert.False(await db.PlaylistSnapshots.AnyAsync(s => s.PlaylistId == playlist.Id));
    }

    private static string SentinelPayload()
    {
        var columns = new[] { new QueryColumn("title", "string") };
        var rows = new object?[][] { ["Sentinel Track"] };
        return JsonSerializer.Serialize(new { Entity = "tracks", Columns = columns, Rows = rows });
    }

    private static string? Cell(QueryPageResponse page, string column)
    {
        var index = -1;
        for (var i = 0; i < page.Columns.Count; i++)
        {
            if (page.Columns[i].Name == column)
            {
                index = i;
            }
        }

        return index < 0 || page.Rows.Count == 0 ? null : ((JsonElement)page.Rows[0][index]!).GetString();
    }

    private bool Skip()
    {
        if (fixture.Available)
        {
            return false;
        }

        output.WriteLine("Postgres not reachable on localhost:5442; skipping snapshot test.");
        return true;
    }
}
