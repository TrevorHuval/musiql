using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MusiQL.Api.Spotify;

public sealed class SpotifyUserClient(HttpClient http, string accessToken) : ISpotifyUserClient
{
    private const int PlaylistBatchSize = 100;
    private const int MaxRetries = 5;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<SpotifyProfile> GetProfileAsync(CancellationToken ct)
    {
        var me = await GetAsync<ProfileDto>("v1/me", ct);
        return new SpotifyProfile(me!.Id, me.DisplayName);
    }

    public async Task<IReadOnlyList<SpotifyTrack>> SearchByIsrcAsync(string isrc, CancellationToken ct) =>
        await SearchAsync($"isrc:{isrc}", 5, ct);

    public async Task<IReadOnlyList<SpotifyTrack>> SearchByTextAsync(string title, string artist, CancellationToken ct)
    {
        var query = $"track:{Quote(title)} artist:{Quote(artist)}";
        return await SearchAsync(query, 10, ct);
    }

    public async Task<SpotifyPlaylistRef> CreatePlaylistAsync(
        string spotifyUserId, string name, string? description, CancellationToken ct)
    {
        var body = new CreatePlaylistDto(name, description ?? "", false);
        var created = await SendAsync<PlaylistDto>(
            HttpMethod.Post, $"v1/users/{Uri.EscapeDataString(spotifyUserId)}/playlists", body, ct);
        return ToRef(created!);
    }

    public async Task<SpotifyPlaylistRef?> GetPlaylistAsync(string playlistId, CancellationToken ct)
    {
        var dto = await GetAsync<PlaylistDto>(
            $"v1/playlists/{Uri.EscapeDataString(playlistId)}?fields=id,name,external_urls(spotify)", ct,
            allowNotFound: true);
        return dto is null ? null : ToRef(dto);
    }

    public async Task UpdatePlaylistDetailsAsync(
        string playlistId, string name, string? description, CancellationToken ct)
    {
        var body = new UpdatePlaylistDto(name, description ?? "");
        await SendAsync<Unit>(HttpMethod.Put, $"v1/playlists/{Uri.EscapeDataString(playlistId)}", body, ct);
    }

    public async Task ReplacePlaylistItemsAsync(string playlistId, IReadOnlyList<string> uris, CancellationToken ct)
    {
        var path = $"v1/playlists/{Uri.EscapeDataString(playlistId)}/tracks";
        var first = uris.Take(PlaylistBatchSize).ToArray();
        await SendAsync<Unit>(HttpMethod.Put, path, new UrisDto(first), ct);

        foreach (var batch in uris.Skip(PlaylistBatchSize).Chunk(PlaylistBatchSize))
        {
            await SendAsync<Unit>(HttpMethod.Post, path, new UrisDto(batch), ct);
        }
    }

    public async Task<SpotifySavedPage> GetSavedTracksAsync(int offset, int limit, CancellationToken ct)
    {
        var page = await GetAsync<SavedPageDto>($"v1/me/tracks?limit={limit}&offset={offset}", ct);
        var items = page!.Items
            .Where(i => i.Track is not null)
            .Select(i => new SpotifySavedItem(
                i.Track!.Id,
                i.Track.Name,
                string.Join(", ", i.Track.Artists.Select(a => a.Name)),
                i.Track.DurationMs,
                i.Track.ExternalIds?.Isrc,
                i.AddedAt))
            .ToList();
        return new SpotifySavedPage(items, page.Total);
    }

    private async Task<IReadOnlyList<SpotifyTrack>> SearchAsync(string query, int limit, CancellationToken ct)
    {
        var url = $"v1/search?type=track&limit={limit}&q={Uri.EscapeDataString(query)}";
        var response = await GetAsync<SearchDto>(url, ct);
        return response!.Tracks.Items.Select(ToTrack).ToList();
    }

    private static SpotifyTrack ToTrack(TrackDto dto) => new(
        dto.Id,
        dto.Uri,
        dto.Name,
        dto.Artists.Select(a => a.Name).ToList(),
        dto.DurationMs,
        dto.ExternalIds?.Isrc);

    private static SpotifyPlaylistRef ToRef(PlaylistDto dto) =>
        new(dto.Id, dto.Name, dto.ExternalUrls?.Spotify ?? $"https://open.spotify.com/playlist/{dto.Id}");

    private static string Quote(string value) => value.Replace("\"", " ").Trim();

    private Task<T?> GetAsync<T>(string path, CancellationToken ct, bool allowNotFound = false) =>
        SendAsync<T>(HttpMethod.Get, path, null, ct, allowNotFound);

    private async Task<T?> SendAsync<T>(
        HttpMethod method, string path, object? body, CancellationToken ct, bool allowNotFound = false)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: Json);
            }

            using var response = await http.SendAsync(request, ct);
            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
            {
                return default;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < MaxRetries)
            {
                await Task.Delay(RetryAfter(response), ct);
                continue;
            }

            if ((int)response.StatusCode >= 500 && attempt < MaxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), ct);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(ct);
                throw new SpotifyApiException((int)response.StatusCode,
                    $"Spotify request failed ({(int)response.StatusCode}): {Truncate(detail)}");
            }

            if (typeof(T) == typeof(Unit) || response.StatusCode == HttpStatusCode.NoContent)
            {
                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>(Json, ct);
        }
    }

    private static TimeSpan RetryAfter(HttpResponseMessage response)
    {
        var seconds = response.Headers.RetryAfter?.Delta?.TotalSeconds
            ?? (double.TryParse(response.Headers.RetryAfter?.ToString(), out var value) ? value : 1);
        return TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 30));
    }

    private static string Truncate(string value) => value.Length <= 300 ? value : value[..300];

    private sealed record Unit;

    private sealed record ProfileDto(string Id, [property: JsonPropertyName("display_name")] string? DisplayName);

    private sealed record SearchDto(TracksDto Tracks);

    private sealed record TracksDto(IReadOnlyList<TrackDto> Items);

    private sealed record TrackDto(
        string Id,
        string Uri,
        string Name,
        IReadOnlyList<ArtistDto> Artists,
        [property: JsonPropertyName("duration_ms")] int? DurationMs,
        [property: JsonPropertyName("external_ids")] ExternalIdsDto? ExternalIds);

    private sealed record ArtistDto(string Name);

    private sealed record ExternalIdsDto(string? Isrc);

    private sealed record ExternalUrlsDto(string? Spotify);

    private sealed record PlaylistDto(
        string Id,
        string Name,
        [property: JsonPropertyName("external_urls")] ExternalUrlsDto? ExternalUrls);

    private sealed record CreatePlaylistDto(string Name, string Description, bool Public);

    private sealed record UpdatePlaylistDto(string Name, string Description);

    private sealed record UrisDto(IReadOnlyList<string> Uris);

    private sealed record SavedPageDto(IReadOnlyList<SavedItemDto> Items, int Total);

    private sealed record SavedItemDto(
        [property: JsonPropertyName("added_at")] DateTime AddedAt,
        TrackDto? Track);
}
