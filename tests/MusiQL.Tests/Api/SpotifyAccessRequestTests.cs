using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MusiQL.Api.Contracts;
using MusiQL.Api.Spotify;
using Xunit.Abstractions;

namespace MusiQL.Tests.Api;

[Collection("api")]
public class SpotifyAccessRequestTests(ApiFixture fixture, ITestOutputHelper output)
{
    private const string Path = "/api/spotify/access-request";

    [Fact]
    public async Task A_request_emails_the_operator_once_and_reports_its_status()
    {
        if (Skip())
        {
            return;
        }

        var mailer = new RecordingMailer();
        using var factory = Factory(mailer);
        var client = await SignedIn(factory);

        var response = await client.PostAsJsonAsync(Path,
            new SpotifyAccessRequestBody("listener@example.com", "Friend of Trevor"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<SpotifyAccessRequestStatus>();
        Assert.True(status!.Enabled);
        Assert.Equal("listener@example.com", status.SpotifyEmail);
        Assert.NotNull(status.RequestedAt);

        var sent = Assert.Single(mailer.Sent);
        Assert.Equal("MusiQL: Spotify access request", sent.Subject);
        Assert.Contains("Spotify account email: listener@example.com", sent.Body);
        Assert.Contains("> Friend of Trevor", sent.Body);

        var again = await client.GetFromJsonAsync<SpotifyAccessRequestStatus>(Path);
        Assert.Equal(status.RequestedAt!.Value, again!.RequestedAt!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task A_second_request_inside_the_cooldown_is_refused_without_email()
    {
        if (Skip())
        {
            return;
        }

        var mailer = new RecordingMailer();
        using var factory = Factory(mailer);
        var client = await SignedIn(factory);

        var first = await client.PostAsJsonAsync(Path, new SpotifyAccessRequestBody("a@example.com", null));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var second = await client.PostAsJsonAsync(Path, new SpotifyAccessRequestBody("b@example.com", null));

        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal("https://musiql.dev/problems/access-request-cooldown", await ProblemType(second));
        Assert.Single(mailer.Sent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("victim@example.com\r\nBcc: someone@example.com")]
    [InlineData("victim@example.com\nSubject: hi")]
    [InlineData("Name <victim@example.com>")]
    [InlineData("a@example.com, b@example.com")]
    [InlineData("user@localhost")]
    public async Task Malformed_or_injected_addresses_are_rejected(string email)
    {
        if (Skip())
        {
            return;
        }

        var mailer = new RecordingMailer();
        using var factory = Factory(mailer);
        var client = await SignedIn(factory);

        var response = await client.PostAsJsonAsync(Path, new SpotifyAccessRequestBody(email, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(mailer.Sent);
    }

    [Fact]
    public async Task An_overlong_note_is_rejected()
    {
        if (Skip())
        {
            return;
        }

        var mailer = new RecordingMailer();
        using var factory = Factory(mailer);
        var client = await SignedIn(factory);

        var response = await client.PostAsJsonAsync(Path,
            new SpotifyAccessRequestBody("ok@example.com", new string('x', 501)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(mailer.Sent);
    }

    [Fact]
    public async Task The_daily_ceiling_holds_across_users()
    {
        if (Skip())
        {
            return;
        }

        int existing;
        using (var db = fixture.CreateAppContext())
        {
            var since = DateTime.UtcNow.AddHours(-24);
            existing = db.SpotifyAccessRequests.Count(r => r.RequestedAt > since);
        }

        var mailer = new RecordingMailer();
        using var factory = Factory(mailer, maxPerDay: existing + 1);
        var first = await SignedIn(factory);
        var second = await SignedIn(factory);

        var ok = await first.PostAsJsonAsync(Path, new SpotifyAccessRequestBody("one@example.com", null));
        var refused = await second.PostAsJsonAsync(Path, new SpotifyAccessRequestBody("two@example.com", null));

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("https://musiql.dev/problems/access-request-limit", await ProblemType(refused));
        Assert.Single(mailer.Sent);
    }

    [Fact]
    public async Task A_failed_send_is_not_recorded_so_the_user_can_retry()
    {
        if (Skip())
        {
            return;
        }

        var mailer = new RecordingMailer { Fail = true };
        using var factory = Factory(mailer);
        var client = await SignedIn(factory);

        var failed = await client.PostAsJsonAsync(Path, new SpotifyAccessRequestBody("retry@example.com", null));
        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);

        mailer.Fail = false;
        var retried = await client.PostAsJsonAsync(Path, new SpotifyAccessRequestBody("retry@example.com", null));
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Single(mailer.Sent);
    }

    [Fact]
    public async Task Requests_are_off_until_configured()
    {
        if (Skip())
        {
            return;
        }

        var client = await fixture.RegisterClientAsync();

        var status = await client.GetFromJsonAsync<SpotifyAccessRequestStatus>(Path);
        Assert.False(status!.Enabled);

        var response = await client.PostAsJsonAsync(Path, new SpotifyAccessRequestBody("x@example.com", null));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_callers_are_turned_away()
    {
        if (Skip())
        {
            return;
        }

        var mailer = new RecordingMailer();
        using var factory = Factory(mailer);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(Path, new SpotifyAccessRequestBody("x@example.com", null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(mailer.Sent);
    }

    [Fact]
    public async Task The_endpoint_is_rate_limited_per_user()
    {
        if (Skip())
        {
            return;
        }

        var mailer = new RecordingMailer();
        using var factory = Factory(mailer, accessRequestsPerHour: 2);
        var client = await SignedIn(factory);

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync(Path, new SpotifyAccessRequestBody("not valid", null));
            statuses.Add(response.StatusCode);
        }

        Assert.Equal([HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.TooManyRequests], statuses);
    }

    [Fact]
    public async Task A_spotify_account_missing_from_the_allowlist_gets_the_invite_only_problem()
    {
        if (Skip())
        {
            return;
        }

        using var factory = new MusiQLApiFactory(fixture.ConnectionString, new Dictionary<string, string>
        {
            ["Spotify:ClientId"] = "test-client",
            ["Spotify:TokenEncryptionKey"] = "VCKRAIzr34U8U8Z1zH3X/IMhM8fcUal/0wVb4D3RBuc="
        }, services =>
        {
            services.AddHttpClient(SpotifyTokenClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() =>
                new StubHandler(HttpStatusCode.OK,
                    "{\"access_token\":\"a\",\"refresh_token\":\"r\",\"expires_in\":3600,\"scope\":\"\"}"));
            services.AddHttpClient(SpotifyClientFactory.HttpClientName).ConfigurePrimaryHttpMessageHandler(() =>
                new StubHandler(HttpStatusCode.Forbidden,
                    "Check settings on developer.spotify.com/dashboard, the user may not be registered."));
        });
        var client = await SignedIn(factory);

        var connect = await client.PostAsync("/api/spotify/connect", null);
        var url = (await connect.Content.ReadFromJsonAsync<SpotifyConnectResponse>())!.AuthorizeUrl;
        var state = Uri.UnescapeDataString(url.Split("state=")[1].Split('&')[0]);

        var callback = await client.PostAsJsonAsync("/api/spotify/callback", new SpotifyCallbackRequest(state, "code"));

        Assert.Equal(HttpStatusCode.Forbidden, callback.StatusCode);
        Assert.Equal("https://musiql.dev/problems/spotify-invite-only", await ProblemType(callback));
    }

    private MusiQLApiFactory Factory(RecordingMailer mailer, int maxPerDay = 100_000, int accessRequestsPerHour = 100_000) =>
        new(fixture.ConnectionString, new Dictionary<string, string>
        {
            ["AccessRequests:NotifyEmail"] = "operator@example.com",
            ["AccessRequests:FromEmail"] = "noreply@example.com",
            ["AccessRequests:SmtpHost"] = "smtp.invalid",
            ["AccessRequests:MaxPerDay"] = maxPerDay.ToString(),
            ["Limits:AccessRequestsPerHour"] = accessRequestsPerHour.ToString()
        }, services => services.Replace(ServiceDescriptor.Singleton<IAccessRequestMailer>(mailer)));

    private async Task<HttpClient> SignedIn(MusiQLApiFactory factory)
    {
        var client = factory.CreateClient();
        var auth = await fixture.RegisterAsync(client, ApiFixture.UniqueEmail());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<string?> ProblemType(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("type", out var type) ? type.GetString() : null;
    }

    private bool Skip()
    {
        if (fixture.Available)
        {
            return false;
        }

        output.WriteLine("Postgres not reachable on localhost:5442; skipping access request test.");
        return true;
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }

    private sealed class RecordingMailer : IAccessRequestMailer
    {
        public bool Fail { get; set; }
        public List<(string Subject, string Body)> Sent { get; } = [];

        public Task SendAsync(string subject, string body, CancellationToken ct)
        {
            if (Fail)
            {
                throw new AccessRequestDeliveryException();
            }

            lock (Sent)
            {
                Sent.Add((subject, body));
            }

            return Task.CompletedTask;
        }
    }
}
