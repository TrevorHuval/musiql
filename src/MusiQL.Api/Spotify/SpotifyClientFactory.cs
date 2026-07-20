namespace MusiQL.Api.Spotify;

public sealed class SpotifyClientFactory(IHttpClientFactory httpFactory, SpotifyAuthService auth) : ISpotifyClientFactory
{
    public const string HttpClientName = "spotify-api";

    public async Task<SpotifyUserSession> ForUserAsync(Guid userId, CancellationToken ct)
    {
        var access = await auth.GetAccessTokenAsync(userId, ct);
        var client = new SpotifyUserClient(httpFactory.CreateClient(HttpClientName), access.AccessToken);
        return new SpotifyUserSession(client, access.SpotifyUserId);
    }
}
