using System.Net;
using System.Net.Http.Json;
using MusiQL.Api.Contracts;
using Xunit.Abstractions;

namespace MusiQL.Tests.Api;

[Collection("api")]
public class RateLimitTests(ApiFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public async Task Preview_throttles_a_burst_from_one_user()
    {
        if (Skip())
        {
            return;
        }

        var client = await fixture.RegisterClientAsync();

        var requests = Enumerable.Range(0, 45).Select(_ =>
            client.PostAsJsonAsync("/api/query/preview", new PreviewRequest("tracks limit 1", null, null)));
        var responses = await Task.WhenAll(requests);

        var throttled = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        var ok = responses.Count(r => r.StatusCode == HttpStatusCode.OK);

        Assert.True(throttled > 0, "expected the burst to trip the rate limiter");
        Assert.True(ok > 0, "expected some requests within the window to succeed");
        Assert.True(ok <= 30, $"permit limit is 30 per window but {ok} succeeded");
    }

    [Fact]
    public async Task Rate_limit_is_per_user()
    {
        if (Skip())
        {
            return;
        }

        var heavy = await fixture.RegisterClientAsync();
        var light = await fixture.RegisterClientAsync();

        var burst = Enumerable.Range(0, 45).Select(_ =>
            heavy.PostAsJsonAsync("/api/query/preview", new PreviewRequest("tracks limit 1", null, null)));
        await Task.WhenAll(burst);

        var response = await light.PostAsJsonAsync(
            "/api/query/preview", new PreviewRequest("tracks limit 1", null, null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private bool Skip()
    {
        if (fixture.Available)
        {
            return false;
        }

        output.WriteLine("Postgres not reachable on localhost:5442; skipping rate limit test.");
        return true;
    }
}
