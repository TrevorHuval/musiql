using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MusiQL.Api.Contracts;
using MusiQL.Data.App;
using Xunit.Abstractions;

namespace MusiQL.Tests.Api;

[Collection("api")]
public class QueryEndpointTests(ApiFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public async Task Preview_requires_authentication()
    {
        if (Skip())
        {
            return;
        }

        var anonymous = fixture.CreateClient();
        var response = await anonymous.PostAsJsonAsync("/api/query/preview", new PreviewRequest("tracks limit 5", null, null));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Preview_runs_the_canonical_grunge_query()
    {
        if (Skip())
        {
            return;
        }

        var client = await fixture.RegisterClientAsync();
        var response = await client.PostAsJsonAsync("/api/query/preview", new PreviewRequest(
            "tracks where genre = \"grunge\" and year between 1990 and 2004 and artist != \"Nirvana\"", null, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<QueryPageResponse>();

        var artists = Column(page!, "artist");
        Assert.Contains("Pearl Jam", artists);
        Assert.DoesNotContain("Nirvana", artists);
    }

    [Fact]
    public async Task Preview_returns_structured_errors_for_invalid_mql()
    {
        if (Skip())
        {
            return;
        }

        var client = await fixture.RegisterClientAsync();
        var response = await client.PostAsJsonAsync(
            "/api/query/preview", new PreviewRequest("tracks where gener = \"grunge\"", null, null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = doc.RootElement.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);

        var first = errors[0];
        Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("code").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("message").GetString()));
        Assert.True(first.GetProperty("start").GetInt32() >= 0);
        Assert.True(first.GetProperty("length").GetInt32() >= 0);
    }

    [Fact]
    public async Task From_library_does_not_leak_between_users()
    {
        if (Skip())
        {
            return;
        }

        var aliceClient = fixture.CreateClient();
        var bobClient = fixture.CreateClient();
        var alice = await fixture.RegisterAsync(aliceClient, ApiFixture.UniqueEmail());
        var bob = await fixture.RegisterAsync(bobClient, ApiFixture.UniqueEmail());
        Authorize(aliceClient, alice.AccessToken);
        Authorize(bobClient, bob.AccessToken);

        Guid[] aliceIds;
        Guid[] bobIds;
        await using (var db = fixture.CreateCatalogContext())
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM app.user_library WHERE user_id IN ({0}, {1})", alice.UserId, bob.UserId);
            var recordings = db.Recordings.OrderBy(r => r.Id).Take(4).ToList();
            db.UserLibrary.Add(new UserLibrary { UserId = alice.UserId, RecordingId = recordings[0].Id });
            db.UserLibrary.Add(new UserLibrary { UserId = alice.UserId, RecordingId = recordings[1].Id });
            db.UserLibrary.Add(new UserLibrary { UserId = bob.UserId, RecordingId = recordings[2].Id });
            db.UserLibrary.Add(new UserLibrary { UserId = bob.UserId, RecordingId = recordings[3].Id });
            await db.SaveChangesAsync();

            aliceIds = [recordings[0].Mbid, recordings[1].Mbid];
            bobIds = [recordings[2].Mbid, recordings[3].Mbid];
        }

        var alicePage = await Preview(aliceClient, "tracks from library");
        var bobPage = await Preview(bobClient, "tracks from library");

        var aliceResult = Column(alicePage, "id").Select(Guid.Parse!).ToHashSet();
        var bobResult = Column(bobPage, "id").Select(Guid.Parse!).ToHashSet();

        Assert.Equal(aliceIds.ToHashSet(), aliceResult);
        Assert.Equal(bobIds.ToHashSet(), bobResult);
        Assert.Empty(aliceResult.Intersect(bobIds));
    }

    [Fact]
    public async Task From_library_reports_empty_library_hint()
    {
        if (Skip())
        {
            return;
        }

        var client = fixture.CreateClient();
        var user = await fixture.RegisterAsync(client, ApiFixture.UniqueEmail());
        Authorize(client, user.AccessToken);

        await using (var db = fixture.CreateCatalogContext())
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM app.user_library WHERE user_id = {0}", user.UserId);
        }

        var page = await Preview(client, "tracks from library");
        Assert.Equal(0, page.Total);
        Assert.Equal("library_empty", page.Hint);
    }

    private async Task<QueryPageResponse> Preview(HttpClient client, string mql)
    {
        var response = await client.PostAsJsonAsync("/api/query/preview", new PreviewRequest(mql, null, null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<QueryPageResponse>())!;
    }

    private static void Authorize(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    private static List<string> Column(QueryPageResponse page, string name)
    {
        var index = -1;
        for (var i = 0; i < page.Columns.Count; i++)
        {
            if (page.Columns[i].Name == name)
            {
                index = i;
            }
        }

        return page.Rows
            .Select(row => (JsonElement)row[index]!)
            .Where(cell => cell.ValueKind != JsonValueKind.Null)
            .Select(cell => cell.ToString())
            .ToList();
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
