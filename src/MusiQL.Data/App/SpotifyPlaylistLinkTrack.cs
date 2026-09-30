namespace MusiQL.Data.App;

// A track MusiQL put on the linked Spotify playlist. Anything on the playlist
// that is not in this set was added by the user and is never touched by an export.
public class SpotifyPlaylistLinkTrack
{
    public Guid PlaylistId { get; set; }
    public string SpotifyUri { get; set; } = "";
}
