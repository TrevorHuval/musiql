namespace MusiQL.Data.Catalog;

public class Artist
{
    public long Id { get; set; }
    public Guid Mbid { get; set; }
    public string Name { get; set; } = "";
    public string SortName { get; set; } = "";
    public short? BeginYear { get; set; }
    public short? EndYear { get; set; }

    public ICollection<ArtistGenre> Genres { get; set; } = [];
}
