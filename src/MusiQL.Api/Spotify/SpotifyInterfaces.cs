namespace MusiQL.Api.Spotify;

public interface ISpotifySearch
{
    Task<IReadOnlyList<SpotifyTrack>> SearchByIsrcAsync(string isrc, CancellationToken ct);
    Task<IReadOnlyList<SpotifyTrack>> SearchByTextAsync(string title, string artist, CancellationToken ct);
}

public interface ISpotifyUserClient : ISpotifySearch
{
    Task<SpotifyProfile> GetProfileAsync(CancellationToken ct);
    Task<SpotifyPlaylistRef> CreatePlaylistAsync(
        string spotifyUserId, string name, string? description, CancellationToken ct);
    Task<SpotifyPlaylistRef?> GetPlaylistAsync(string playlistId, CancellationToken ct);
    Task UpdatePlaylistDetailsAsync(string playlistId, string name, string? description, CancellationToken ct);
    Task ReplacePlaylistItemsAsync(string playlistId, IReadOnlyList<string> uris, CancellationToken ct);
    Task<SpotifySavedPage> GetSavedTracksAsync(int offset, int limit, CancellationToken ct);
}

public interface ISpotifyClientFactory
{
    Task<SpotifyUserSession> ForUserAsync(Guid userId, CancellationToken ct);
}

public interface IMusicBrainzIsrcLookup
{
    Task<IReadOnlyList<string>> GetIsrcsAsync(Guid recordingMbid, CancellationToken ct);
}
