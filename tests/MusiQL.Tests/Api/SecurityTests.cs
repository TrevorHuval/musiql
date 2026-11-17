using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using MusiQL.Api.Contracts;
using MusiQL.Api.Spotify;
using MusiQL.Data.App;
using Xunit.Abstractions;

namespace MusiQL.Tests.Api;

[Collection("api")]
public class SecurityTests(ApiFixture fixture, ITestOutputHelper output)
{
    // Matches MusiQLApiFactory.
    private const string SigningKey = "test-signing-key-that-is-definitely-long-enough-0123456789abcdef";

    [Fact]
    public async Task Login_for_an_unknown_address_costs_a_password_hash_like_a_wrong_password()
    {
        if (Skip())
        {
            return;
        }

        var hasher = new CountingHasher();
        using var factory = new MusiQLApiFactory(fixture.ConnectionString, services: services =>
            services.Replace(ServiceDescriptor.Scoped<IPasswordHasher<AppUser>>(_ => hasher)));
        var client = factory.CreateClient();
        var email = ApiFixture.UniqueEmail();
        await fixture.RegisterAsync(client, email);

        hasher.Reset();
        var wrong = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword1"));
        var wrongWork = hasher.Operations;

        hasher.Reset();
        var unknown = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(ApiFixture.UniqueEmail(), "WrongPassword1"));
        var unknownWork = hasher.Operations;

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(await Problem(wrong, "type"), await Problem(unknown, "type"));
        Assert.Equal(await Problem(wrong, "title"), await Problem(unknown, "title"));
        Assert.Equal(1, wrongWork);
        Assert.Equal(wrongWork, unknownWork);
    }

    [Theory]
    [InlineData(SecurityAlgorithms.HmacSha256, HttpStatusCode.OK)]
    [InlineData(SecurityAlgorithms.HmacSha384, HttpStatusCode.Unauthorized)]
    public async Task Only_the_issued_signing_algorithm_is_accepted(string algorithm, HttpStatusCode expected)
    {
        if (Skip())
        {
            return;
        }

        var client = fixture.CreateClient();
        var auth = await fixture.RegisterAsync(client, ApiFixture.UniqueEmail());

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var token = new JwtSecurityToken(
            "musiql-test",
            "musiql-test",
            [new Claim(JwtRegisteredClaimNames.Sub, auth.UserId.ToString())],
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(key, algorithm));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtSecurityTokenHandler().WriteToken(token));

        var response = await client.GetAsync("/api/playlists");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task A_spotify_state_is_spent_by_a_failed_code_exchange()
    {
        if (Skip())
        {
            return;
        }

        using var factory = SpotifyFactory(HttpStatusCode.BadRequest, new string('x', 5000));
        var client = await SignedIn(factory);
        var state = await ConnectAsync(client);

        var first = await client.PostAsJsonAsync("/api/spotify/callback", new SpotifyCallbackRequest(state, "bad"));
        var second = await client.PostAsJsonAsync("/api/spotify/callback", new SpotifyCallbackRequest(state, "good"));

        Assert.Equal(HttpStatusCode.BadGateway, first.StatusCode);
        Assert.Equal("https://musiql.dev/problems/spotify-upstream", await Problem(first, "type"));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal("https://musiql.dev/problems/spotify-auth-state", await Problem(second, "type"));
    }

    [Fact]
    public async Task An_upstream_token_error_is_only_quoted_in_part()
    {
        if (Skip())
        {
            return;
        }

        using var factory = SpotifyFactory(HttpStatusCode.BadRequest, new string('x', 5000));
        var client = await SignedIn(factory);
        var state = await ConnectAsync(client);

        var response = await client.PostAsJsonAsync("/api/spotify/callback", new SpotifyCallbackRequest(state, "bad"));

        var detail = await Problem(response, "detail");
        Assert.NotNull(detail);
        Assert.InRange(detail.Length, 1, 400);
    }

    [Fact]
    public async Task A_spotify_state_cannot_be_completed_by_another_user()
    {
        if (Skip())
        {
            return;
        }

        using var factory = SpotifyFactory(HttpStatusCode.OK,
            "{\"access_token\":\"a\",\"refresh_token\":\"r\",\"expires_in\":3600,\"scope\":\"\"}");
        var owner = await SignedIn(factory);
        var other = await SignedIn(factory);
        var state = await ConnectAsync(owner);

        var stolen = await other.PostAsJsonAsync("/api/spotify/callback", new SpotifyCallbackRequest(state, "code"));
        var status = await other.GetFromJsonAsync<SpotifyConnectionStatus>("/api/spotify/status");

        Assert.Equal(HttpStatusCode.BadRequest, stolen.StatusCode);
        Assert.Equal("https://musiql.dev/problems/spotify-auth-state", await Problem(stolen, "type"));
        Assert.False(status!.Connected);
    }

    private MusiQLApiFactory SpotifyFactory(HttpStatusCode tokenStatus, string tokenBody) =>
        new(fixture.ConnectionString, new Dictionary<string, string>
        {
            ["Spotify:ClientId"] = "test-client",
            ["Spotify:TokenEncryptionKey"] = "VCKRAIzr34U8U8Z1zH3X/IMhM8fcUal/0wVb4D3RBuc="
        }, services =>
        {
            services.AddHttpClient(SpotifyTokenClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() =>
                new StubHandler(tokenStatus, tokenBody));
            services.AddHttpClient(SpotifyClientFactory.HttpClientName).ConfigurePrimaryHttpMessageHandler(() =>
                new StubHandler(HttpStatusCode.OK, "{\"id\":\"spotify-user\",\"display_name\":\"Listener\"}"));
        });

    private async Task<HttpClient> SignedIn(MusiQLApiFactory factory)
    {
        var client = factory.CreateClient();
        var auth = await fixture.RegisterAsync(client, ApiFixture.UniqueEmail());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<string> ConnectAsync(HttpClient client)
    {
        var connect = await client.PostAsync("/api/spotify/connect", null);
        connect.EnsureSuccessStatusCode();
        var url = (await connect.Content.ReadFromJsonAsync<SpotifyConnectResponse>())!.AuthorizeUrl;
        return Uri.UnescapeDataString(url.Split("state=")[1].Split('&')[0]);
    }

    private static async Task<string?> Problem(HttpResponseMessage response, string property)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty(property, out var value) ? value.GetString() : null;
    }

    private bool Skip()
    {
        if (fixture.Available)
        {
            return false;
        }

        output.WriteLine("Postgres not reachable on localhost:5442; skipping security test.");
        return true;
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }

    private sealed class CountingHasher : IPasswordHasher<AppUser>
    {
        private readonly PasswordHasher<AppUser> _inner = new();
        private int _operations;

        public int Operations => _operations;

        public void Reset() => _operations = 0;

        public string HashPassword(AppUser user, string password)
        {
            Interlocked.Increment(ref _operations);
            return _inner.HashPassword(user, password);
        }

        public PasswordVerificationResult VerifyHashedPassword(
            AppUser user, string hashedPassword, string providedPassword)
        {
            Interlocked.Increment(ref _operations);
            return _inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }
}
