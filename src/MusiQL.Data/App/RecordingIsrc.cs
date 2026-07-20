namespace MusiQL.Data.App;

public class RecordingIsrc
{
    public Guid RecordingMbid { get; set; }
    public string? Isrc { get; set; }
    public DateTime FetchedAt { get; set; }
}
