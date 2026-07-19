namespace MusiQL.Data.Catalog;

public class ReleaseGroupGenre
{
    public long ReleaseGroupId { get; set; }
    public long GenreId { get; set; }
    public int Votes { get; set; }

    public ReleaseGroup? ReleaseGroup { get; set; }
    public Genre? Genre { get; set; }
}
