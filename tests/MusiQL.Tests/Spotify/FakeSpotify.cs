using MusiQL.Api.Spotify;

namespace MusiQL.Tests.Spotify;

public sealed class FakeSpotifyClient : ISpotifyUserClient
{
    public string SpotifyUserId { get; init; } = "spotify-user";
    public Func<string, string, IReadOnlyList<SpotifyTrack>> TextSearch { get; set; } = (_, _) => [];
    public Func<string, IReadOnlyList<SpotifyTrack>> IsrcSearch { get; set; } = _ => [];
    public List<SpotifySavedItem> Saved { get; set; } = [];

    public List<(string Id, string Name, string? Description)> Created { get; } = [];
    public Dictionary<string, List<string>> Replaced { get; } = [];
    public HashSet<string> ExistingPlaylists { get; } = [];
    public int TextSearchCalls { get; private set; }
    public int IsrcSearchCalls { get; private set; }

    private int _counter;

    public Task<SpotifyProfile> GetProfileAsync(CancellationToken ct) =>
        Task.FromResult(new SpotifyProfile(SpotifyUserId, "Fake User"));

    public Task<IReadOnlyList<SpotifyTrack>> SearchByIsrcAsync(string isrc, CancellationToken ct)
    {
        IsrcSearchCalls++;
        return Task.FromResult(IsrcSearch(isrc));
    }

    public Task<IReadOnlyList<SpotifyTrack>> SearchByTextAsync(string title, string artist, CancellationToken ct)
    {
        TextSearchCalls++;
        return Task.FromResult(TextSearch(title, artist));
    }

    public Task<SpotifyPlaylistRef> CreatePlaylistAsync(
        string spotifyUserId, string name, string? description, CancellationToken ct)
    {
        var id = $"pl{++_counter}";
        Created.Add((id, name, description));
        ExistingPlaylists.Add(id);
        return Task.FromResult(new SpotifyPlaylistRef(id, name, $"https://open.spotify.com/playlist/{id}"));
    }

    public Task<SpotifyPlaylistRef?> GetPlaylistAsync(string playlistId, CancellationToken ct) =>
        Task.FromResult(ExistingPlaylists.Contains(playlistId)
            ? new SpotifyPlaylistRef(playlistId, "existing", $"https://open.spotify.com/playlist/{playlistId}")
            : null);

    public Task UpdatePlaylistDetailsAsync(
        string playlistId, string name, string? description, CancellationToken ct) => Task.CompletedTask;

    public Task ReplacePlaylistItemsAsync(string playlistId, IReadOnlyList<string> uris, CancellationToken ct)
    {
        Replaced[playlistId] = uris.ToList();
        return Task.CompletedTask;
    }

    public Task<SpotifySavedPage> GetSavedTracksAsync(int offset, int limit, CancellationToken ct)
    {
        var items = Saved.Skip(offset).Take(limit).ToList();
        return Task.FromResult(new SpotifySavedPage(items, Saved.Count));
    }
}

public sealed class FakeSpotifyClientFactory(FakeSpotifyClient client) : ISpotifyClientFactory
{
    public Task<SpotifyUserSession> ForUserAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult(new SpotifyUserSession(client, client.SpotifyUserId));
}

public sealed class FakeIsrcLookup : IMusicBrainzIsrcLookup
{
    public Dictionary<Guid, IReadOnlyList<string>> Map { get; } = [];

    public Task<IReadOnlyList<string>> GetIsrcsAsync(Guid recordingMbid, CancellationToken ct) =>
        Task.FromResult(Map.TryGetValue(recordingMbid, out var isrcs) ? isrcs : []);
}
