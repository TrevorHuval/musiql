using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace MusiQL.Api.Spotify;

public sealed record SpotifyTokens(string AccessToken, string? RefreshToken, int ExpiresIn, string Scope);

public sealed class SpotifyTokenClient(IHttpClientFactory httpFactory, IOptions<SpotifyOptions> options)
{
    public const string HttpClientName = "spotify-accounts";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<SpotifyTokens> ExchangeCodeAsync(
        string code, string codeVerifier, string redirectUri, CancellationToken ct) =>
        PostAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = options.Value.ClientId,
            ["code_verifier"] = codeVerifier
        }, ct);

    public Task<SpotifyTokens> RefreshAsync(string refreshToken, CancellationToken ct) =>
        PostAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = options.Value.ClientId
        }, ct);

    private async Task<SpotifyTokens> PostAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        var http = httpFactory.CreateClient(HttpClientName);
        using var content = new FormUrlEncodedContent(form);
        using var response = await http.PostAsync("api/token", content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new SpotifyApiException((int)response.StatusCode, $"Spotify token request failed: {body}");
        }

        var dto = JsonSerializer.Deserialize<TokenDto>(body, Json)
            ?? throw new SpotifyApiException(502, "Spotify returned an empty token response.");
        return new SpotifyTokens(dto.AccessToken, dto.RefreshToken, dto.ExpiresIn, dto.Scope ?? "");
    }

    private sealed record TokenDto(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("scope")] string? Scope);
}
