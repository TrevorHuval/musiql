namespace MusiQL.Api.Spotify;

public sealed record SpotifyTrack(
    string Id,
    string Uri,
    string Name,
    IReadOnlyList<string> Artists,
    int? DurationMs,
    string? Isrc);

public sealed record SpotifyProfile(string Id, string? DisplayName);

public sealed record SpotifyUserSession(ISpotifyUserClient Client, string SpotifyUserId);

public sealed record SpotifyAccessContext(string AccessToken, string SpotifyUserId);

public sealed record SpotifyPlaylistRef(string Id, string Name, string Url);

public sealed record SpotifySavedItem(
    string TrackId,
    string Title,
    string Artist,
    int? DurationMs,
    string? Isrc,
    DateTime AddedAt);

public sealed record SpotifySavedPage(IReadOnlyList<SpotifySavedItem> Items, int Total);

public sealed record RecordingRef(Guid Mbid, string Title, string Artist, int? DurationMs);

public sealed record CatalogMatch(long RecordingId, Guid Mbid, double Confidence);

public enum MatchMethod
{
    None,
    Isrc,
    Search
}

public sealed record TrackMatchResult(
    string? SpotifyTrackId,
    string? SpotifyUri,
    double Confidence,
    MatchMethod Method,
    string? Isrc)
{
    public bool Matched => SpotifyTrackId is not null;

    public static TrackMatchResult Unmatched(double confidence = 0) =>
        new(null, null, confidence, MatchMethod.None, null);
}
