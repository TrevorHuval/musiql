namespace MusiQL.Data.App;

public class TrackMatch
{
    public Guid RecordingMbid { get; set; }
    public string? SpotifyTrackId { get; set; }
    public string? SpotifyUri { get; set; }
    public string? Isrc { get; set; }
    public double Confidence { get; set; }
    public string Method { get; set; } = "";
    public DateTime MatchedAt { get; set; }
}
