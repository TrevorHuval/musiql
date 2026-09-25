namespace MusiQL.Data.App;

public class SpotifyPlaylistLink
{
    public Guid PlaylistId { get; set; }
    public Guid UserId { get; set; }
    public string SpotifyPlaylistId { get; set; } = "";
    public int TrackCount { get; set; }
    public DateTime LastExportedAt { get; set; }

    // Re-export to Spotify once a day so the Spotify copy follows the query.
    public bool KeepLive { get; set; }
    public DateTime? LastRefreshAttemptAt { get; set; }
    public string? LastRefreshError { get; set; }
}
