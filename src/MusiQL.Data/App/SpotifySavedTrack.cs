namespace MusiQL.Data.App;

public class SpotifySavedTrack
{
    public Guid UserId { get; set; }
    public string SpotifyTrackId { get; set; } = "";
    public string? Isrc { get; set; }
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public int? DurationMs { get; set; }
    public DateTime AddedAt { get; set; }
    public long? RecordingId { get; set; }
    public Guid? RecordingMbid { get; set; }
    public double? Confidence { get; set; }
    public DateTime SyncedAt { get; set; }

    // Last time matching ran and found nothing; unmatched tracks are not
    // retried on every sync.
    public DateTime? MatchAttemptedAt { get; set; }
}
