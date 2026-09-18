using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MusiQL.Api.Contracts;
using Xunit.Abstractions;

namespace MusiQL.Tests.Api;

[Collection("api")]
public class HardeningTests(ApiFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public async Task Concurrent_refreshes_of_one_token_yield_a_single_successor()
    {
        if (Skip())
        {
            return;
        }

        var client = fixture.CreateClient();
        var auth = await fixture.RegisterAsync(client, ApiFixture.UniqueEmail());

        var attempts = Enumerable.Range(0, 8).Select(_ =>
            client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken)));
        var responses = await Task.WhenAll(attempts);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task Replaying_a_consumed_token_revokes_the_whole_session_family()
    {
        if (Skip())
        {
            return;
        }

        var client = fixture.CreateClient();
        var first = await fixture.RegisterAsync(client, ApiFixture.UniqueEmail());

        var rotated = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(first.RefreshToken));
        var second = (await rotated.Content.ReadFromJsonAsync<AuthResponse>())!;

        var replay = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(first.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        var successor = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(second.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, successor.StatusCode);
    }

    [Fact]
    public async Task Live_sessions_per_user_are_capped()
    {
        if (Skip())
        {
            return;
        }

        var email = ApiFixture.UniqueEmail();
        var client = fixture.CreateClient();
        var auth = await fixture.RegisterAsync(client, email);

        for (var i = 0; i < 25; i++)
        {
            var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
            login.EnsureSuccessStatusCode();
        }

        await using var db = fixture.CreateAppContext();
        var now = DateTime.UtcNow;
        var active = await db.RefreshTokens.CountAsync(t =>
            t.UserId == auth.UserId && t.RevokedAt == null && t.ExpiresAt > now);
        Assert.Equal(20, active);
    }

    [Fact]
    public async Task Oversized_credentials_are_rejected_before_hashing()
    {
        if (Skip())
        {
            return;
        }

        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest(ApiFixture.UniqueEmail(), new string('A', 129) + "a1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Playlist_fields_are_bounded_before_reaching_the_database()
    {
        if (Skip())
        {
            return;
        }

        var client = await fixture.RegisterClientAsync();

        var longName = await client.PostAsJsonAsync(
            "/api/playlists", new PlaylistRequest(new string('n', 201), null, "tracks limit 1"));
        Assert.Equal(HttpStatusCode.BadRequest, longName.StatusCode);

        var longMql = await client.PostAsJsonAsync(
            "/api/playlists", new PlaylistRequest("x", null, "tracks where " + new string(' ', 8000) + "year = 1"));
        Assert.Equal(HttpStatusCode.BadRequest, longMql.StatusCode);

        var longPreview = await client.PostAsJsonAsync(
            "/api/query/preview", new PreviewRequest("tracks where " + new string(' ', 8000) + "year = 1", null, null));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, longPreview.StatusCode);
    }

    [Fact]
    public async Task Playlist_quota_and_registration_cap_are_enforced()
    {
        if (Skip())
        {
            return;
        }

        using var factory = new MusiQLApiFactory(fixture.ConnectionString, new Dictionary<string, string>
        {
            ["Limits:MaxPlaylistsPerUser"] = "2",
            ["Limits:RegistrationsPerHour"] = "2"
        });

        var client = factory.CreateClient();
        var auth = await fixture.RegisterAsync(client, ApiFixture.UniqueEmail());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        for (var i = 0; i < 2; i++)
        {
            var ok = await client.PostAsJsonAsync(
                "/api/playlists", new PlaylistRequest($"p{i}", null, "tracks limit 1"));
            Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        }

        var third = await client.PostAsJsonAsync("/api/playlists", new PlaylistRequest("p2", null, "tracks limit 1"));
        Assert.Equal(HttpStatusCode.Forbidden, third.StatusCode);

        var anonymous = factory.CreateClient();
        await fixture.RegisterAsync(anonymous, ApiFixture.UniqueEmail());
        var throttled = await anonymous.PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest(ApiFixture.UniqueEmail(), "Password123!"));
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }

    [Fact]
    public async Task Query_admission_turns_away_overflow_with_retry_after()
    {
        if (Skip())
        {
            return;
        }

        using var factory = new MusiQLApiFactory(fixture.ConnectionString, new Dictionary<string, string>
        {
            ["Query:MaxConcurrent"] = "1",
            ["Query:AdmissionTimeout"] = "00:00:00.050",
            ["Query:StatementTimeout"] = "00:00:05"
        });

        var client = factory.CreateClient();
        var auth = await fixture.RegisterAsync(client, ApiFixture.UniqueEmail());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Enough parallel work that at least one request finds the single slot
        // occupied for longer than the admission timeout.
        var slow = "tracks where title contains \"a\" or artist contains \"e\" order by year desc";
        var burst = Enumerable.Range(0, 12).Select(_ =>
            client.PostAsJsonAsync("/api/query/preview", new PreviewRequest(slow, null, null)));
        var responses = await Task.WhenAll(burst);

        var busy = responses.Where(r => r.StatusCode == HttpStatusCode.ServiceUnavailable).ToList();
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(busy, r => Assert.NotNull(r.Headers.RetryAfter));
    }

    [Fact]
    public async Task Health_reports_catalog_presence()
    {
        if (Skip())
        {
            return;
        }

        var report = await fixture.CreateClient().GetFromJsonAsync<Dictionary<string, object>>("/api/health");
        Assert.True(report!["catalogLoaded"] is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.True });
    }

    private bool Skip()
    {
        if (fixture.Available)
        {
            return false;
        }

        output.WriteLine("Postgres not reachable on localhost:5442; skipping hardening test.");
        return true;
    }
}
