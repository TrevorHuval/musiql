namespace MusiQL.Data.App;

public class PlaylistSnapshot
{
    public Guid PlaylistId { get; set; }
    public string MqlText { get; set; } = "";
    public DateTime ComputedAt { get; set; }
    public string Payload { get; set; } = "";
}
