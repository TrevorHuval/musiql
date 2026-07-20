using System.Net;
using System.Net.Http.Json;
using MusiQL.Api.Contracts;
using Xunit.Abstractions;

namespace MusiQL.Tests.Api;

[Collection("api")]
public class M3uExportTests(ApiFixture fixture, ITestOutputHelper output)
{
    private const string GrungeMql =
        "tracks where genre = \"grunge\" and year between 1990 and 2004 and artist != \"Nirvana\"";

    [Fact]
    public async Task Exports_a_track_playlist_as_m3u()
    {
        if (Skip())
        {
            return;
        }

        var client = await fixture.RegisterClientAsync();
        var created = await client.PostAsJsonAsync("/api/playlists", new PlaylistRequest("Grunge", null, GrungeMql));
        var playlist = (await created.Content.ReadFromJsonAsync<PlaylistResponse>())!;

        var response = await client.GetAsync($"/api/playlists/{playlist.Id}/export/m3u");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("audio/x-mpegurl", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Grunge.m3u8", response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName);

        var body = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("#EXTM3U", body);
        Assert.Contains("#EXTINF:", body);
        Assert.Contains("Pearl Jam", body);
        Assert.Contains("https://musicbrainz.org/recording/", body);
        Assert.DoesNotContain("Nirvana", body);
    }

    [Fact]
    public async Task Rejects_album_playlists()
    {
        if (Skip())
        {
            return;
        }

        var client = await fixture.RegisterClientAsync();
        var created = await client.PostAsJsonAsync(
            "/api/playlists", new PlaylistRequest("Albums", null, "albums where genre = \"grunge\""));
        var playlist = (await created.Content.ReadFromJsonAsync<PlaylistResponse>())!;

        var response = await client.GetAsync($"/api/playlists/{playlist.Id}/export/m3u");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Requires_ownership()
    {
        if (Skip())
        {
            return;
        }

        var alice = await fixture.RegisterClientAsync();
        var bob = await fixture.RegisterClientAsync();
        var created = await alice.PostAsJsonAsync("/api/playlists", new PlaylistRequest("Mine", null, "tracks limit 5"));
        var playlist = (await created.Content.ReadFromJsonAsync<PlaylistResponse>())!;

        var response = await bob.GetAsync($"/api/playlists/{playlist.Id}/export/m3u");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private bool Skip()
    {
        if (fixture.Available)
        {
            return false;
        }

        output.WriteLine("Postgres not reachable on localhost:5442; skipping M3U export test.");
        return true;
    }
}
