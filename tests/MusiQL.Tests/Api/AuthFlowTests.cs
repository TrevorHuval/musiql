using System.Net;
using System.Net.Http.Json;
using MusiQL.Api.Contracts;
using Xunit.Abstractions;

namespace MusiQL.Tests.Api;

[Collection("api")]
public class AuthFlowTests(ApiFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public async Task Register_returns_tokens_and_login_succeeds()
    {
        if (Skip())
        {
            return;
        }

        var email = ApiFixture.UniqueEmail();
        var client = fixture.CreateClient();

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var registered = await register.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(registered!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(registered.RefreshToken));

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loggedIn = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal(registered.UserId, loggedIn!.UserId);
    }

    [Fact]
    public async Task Register_rejects_weak_password()
    {
        if (Skip())
        {
            return;
        }

        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest(ApiFixture.UniqueEmail(), "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Duplicate_email_is_rejected()
    {
        if (Skip())
        {
            return;
        }

        var email = ApiFixture.UniqueEmail();
        var client = fixture.CreateClient();
        await fixture.RegisterAsync(client, email);

        var second = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_is_unauthorized()
    {
        if (Skip())
        {
            return;
        }

        var email = ApiFixture.UniqueEmail();
        var client = fixture.CreateClient();
        await fixture.RegisterAsync(client, email);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword1!"));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Account_locks_after_repeated_failures()
    {
        if (Skip())
        {
            return;
        }

        var email = ApiFixture.UniqueEmail();
        var client = fixture.CreateClient();
        await fixture.RegisterAsync(client, email);

        HttpResponseMessage? last = null;
        for (var i = 0; i < 5; i++)
        {
            last = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword1!"));
        }

        Assert.Equal(HttpStatusCode.Locked, last!.StatusCode);

        var afterLock = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.Locked, afterLock.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_and_invalidates_the_used_token()
    {
        if (Skip())
        {
            return;
        }

        var client = fixture.CreateClient();
        var tokens = await fixture.RegisterAsync(client, ApiFixture.UniqueEmail());

        var first = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var rotated = await first.Content.ReadFromJsonAsync<AuthResponse>();

        var withNew = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(rotated!.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, withNew.StatusCode);

        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task Session_cap_deletes_oldest_token_without_revoking_the_rest()
    {
        if (Skip())
        {
            return;
        }

        using var factory = new MusiQLApiFactory(fixture.ConnectionString, new Dictionary<string, string>
        {
            ["Limits:MaxActiveRefreshTokensPerUser"] = "1"
        });
        var client = factory.CreateClient();
        var email = ApiFixture.UniqueEmail();
        var first = await fixture.RegisterAsync(client, email);
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        var second = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        var trimmed = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(first.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, trimmed.StatusCode);

        var survivor = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(second!.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, survivor.StatusCode);
    }

    [Fact]
    public async Task Registration_past_user_cap_is_forbidden()
    {
        if (Skip())
        {
            return;
        }

        using var factory = new MusiQLApiFactory(fixture.ConnectionString, new Dictionary<string, string>
        {
            ["Limits:MaxUsers"] = "1"
        });
        var client = factory.CreateClient();

        // The database is shared, so at least one user may already exist.
        var response = await client.PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest(ApiFixture.UniqueEmail(), "Password123!"));
        if (response.StatusCode == HttpStatusCode.Created)
        {
            response = await client.PostAsJsonAsync(
                "/api/auth/register", new RegisterRequest(ApiFixture.UniqueEmail(), "Password123!"));
        }

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
