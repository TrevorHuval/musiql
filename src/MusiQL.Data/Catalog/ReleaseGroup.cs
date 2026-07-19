namespace MusiQL.Data.Catalog;

public class ReleaseGroup
{
    public long Id { get; set; }
    public Guid Mbid { get; set; }
    public string Name { get; set; } = "";
    public long ArtistId { get; set; }
    public string? PrimaryType { get; set; }
    public short? FirstReleaseYear { get; set; }

    public Artist? Artist { get; set; }
    public ICollection<ReleaseGroupGenre> Genres { get; set; } = [];
}
