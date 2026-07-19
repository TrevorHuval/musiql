using System.Net;
using System.Net.Http.Json;
using MusiQL.Api.Contracts;
using Xunit.Abstractions;

namespace MusiQL.Tests.Api;

[Collection("api")]
public class PlaylistTests(ApiFixture fixture, ITestOutputHelper output)
{
    private const string GrungeMql =
        "tracks where genre = \"grunge\" and year between 1990 and 2004 and artist != \"Nirvana\"";

    [Fact]
    public async Task Create_then_read_and_update_and_delete()
    {
        if (Skip())
        {
            return;
        }

        var client = await fixture.RegisterClientAsync();

        var created = await client.PostAsJsonAsync(
            "/api/playlists", new PlaylistRequest("Grunge", "90s grunge", GrungeMql));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var playlist = await created.Content.ReadFromJsonAsync<PlaylistResponse>();
        Assert.Equal("Grunge", playlist!.Name);

        var fetched = await client.GetFromJsonAsync<PlaylistResponse>($"/api/playlists/{playlist.Id}");
        Assert.Equal(playlist.Id, fetched!.Id);

        var list = await client.GetFromJsonAsync<List<PlaylistResponse>>("/api/playlists");
        Assert.Contains(list!, p => p.Id == playlist.Id);

        var updated = await client.PutAsJsonAsync(
            $"/api/playlists/{playlist.Id}", new PlaylistRequest("Grunge v2", null, GrungeMql));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var updatedBody = await updated.Content.ReadFromJsonAsync<PlaylistResponse>();
        Assert.Equal("Grunge v2", updatedBody!.Name);

        var deleted = await client.DeleteAsync($"/api/playlists/{playlist.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var afterDelete = await client.GetAsync($"/api/playlists/{playlist.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_invalid_mql_with_positional_errors()
    {
        if (Skip())
        {
            return;
        }

        var client = await fixture.RegisterClientAsync();
        var response = await client.PostAsJsonAsync(
            "/api/playlists", new PlaylistRequest("Broken", null, "tracks where year >< 1990"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Requires_authentication()
    {
        if (Skip())
        {
            return;
        }

        var anonymous = fixture.CreateClient();
        var response = await anonymous.GetAsync("/api/playlists");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Owner_scoping_hides_other_users_playlists()
    {
        if (Skip())
        {
            return;
        }

        var alice = await fixture.RegisterClientAsync();
        var bob = await fixture.RegisterClientAsync();

        var created = await alice.PostAsJsonAsync(
            "/api/playlists", new PlaylistRequest("Alice list", null, "tracks limit 10"));
        var playlist = await created.Content.ReadFromJsonAsync<PlaylistResponse>();

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/playlists/{playlist!.Id}")).StatusCode);

        var bobUpdate = await bob.PutAsJsonAsync(
            $"/api/playlists/{playlist.Id}", new PlaylistRequest("Hijacked", null, "tracks limit 10"));
        Assert.Equal(HttpStatusCode.NotFound, bobUpdate.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/playlists/{playlist.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/playlists/{playlist.Id}/tracks")).StatusCode);

        var bobList = await bob.GetFromJsonAsync<List<PlaylistResponse>>("/api/playlists");
        Assert.DoesNotContain(bobList!, p => p.Id == playlist.Id);

        Assert.Equal(HttpStatusCode.OK, (await alice.GetAsync($"/api/playlists/{playlist.Id}")).StatusCode);
    }

    private bool Skip()
    {
        if (fixture.Available)
        {
            return false;
        }

        output.WriteLine("Postgres not reachable on localhost:5442; skipping API test.");
        return true;
    }
}
