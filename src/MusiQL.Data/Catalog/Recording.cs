namespace MusiQL.Data.Catalog;

public class Recording
{
    public long Id { get; set; }
    public Guid Mbid { get; set; }
    public string Name { get; set; } = "";
    public long ArtistId { get; set; }
    public int? LengthMs { get; set; }
    public long? ReleaseGroupId { get; set; }
    public short? FirstReleaseYear { get; set; }

    public Artist? Artist { get; set; }
    public ReleaseGroup? ReleaseGroup { get; set; }
    public ICollection<RecordingGenre> Genres { get; set; } = [];
}
