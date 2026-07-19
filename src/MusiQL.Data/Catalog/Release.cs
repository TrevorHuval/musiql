namespace MusiQL.Data.Catalog;

public class Release
{
    public long Id { get; set; }
    public Guid Mbid { get; set; }
    public string Name { get; set; } = "";
    public long ReleaseGroupId { get; set; }
    public long ArtistId { get; set; }

    public ReleaseGroup? ReleaseGroup { get; set; }
    public Artist? Artist { get; set; }
}
