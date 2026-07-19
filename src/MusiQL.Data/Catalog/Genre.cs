namespace MusiQL.Data.Catalog;

public class Genre
{
    public long Id { get; set; }
    public Guid Mbid { get; set; }
    public string Name { get; set; } = "";
}
