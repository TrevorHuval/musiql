namespace MusiQL.Data.App;

public class SpotifyPlaylistLink
{
    public Guid PlaylistId { get; set; }
    public Guid UserId { get; set; }
    public string SpotifyPlaylistId { get; set; } = "";
    public int TrackCount { get; set; }
    public DateTime LastExportedAt { get; set; }
}
