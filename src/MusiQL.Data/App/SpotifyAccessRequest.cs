namespace MusiQL.Data.App;

// A user's request to be added to the Spotify app's allowlist. One row per
// user; a repeat request after the cooldown replaces it.
public class SpotifyAccessRequest
{
    public const int MaxEmailLength = 254;
    public const int MaxNoteLength = 500;

    public Guid UserId { get; set; }
    public string SpotifyEmail { get; set; } = "";
    public string? Note { get; set; }
    public DateTime RequestedAt { get; set; }
}
