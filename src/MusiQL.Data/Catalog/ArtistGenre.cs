namespace MusiQL.Data.Catalog;

public class ArtistGenre
{
    public long ArtistId { get; set; }
    public long GenreId { get; set; }
    public int Votes { get; set; }

    public Artist? Artist { get; set; }
    public Genre? Genre { get; set; }
}
