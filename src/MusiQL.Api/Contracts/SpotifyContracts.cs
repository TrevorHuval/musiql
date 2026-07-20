namespace MusiQL.Api.Contracts;

public sealed record SpotifyConnectResponse(string AuthorizeUrl);

public sealed record SpotifyCallbackRequest(string State, string Code);

public sealed record SpotifyConnectionStatus(
    bool Connected,
    string? DisplayName,
    string? SpotifyUserId,
    DateTime? ConnectedAt,
    DateTime? LibrarySyncedAt,
    int LibrarySavedCount,
    int LibraryMatchedCount);

public sealed record UnmatchedTrackDto(string Title, string Artist, int? Year, double Confidence);

public sealed record ExportResponse(
    string PlaylistName,
    string SpotifyPlaylistId,
    string SpotifyUrl,
    int TrackCount,
    int MatchedCount,
    int TotalCount,
    IReadOnlyList<UnmatchedTrackDto> Unmatched,
    DateTime ExportedAt);

public sealed record LibrarySyncResponse(
    int SavedCount,
    int MatchedCount,
    int UnmatchedCount,
    int LibraryCount,
    DateTime SyncedAt);
