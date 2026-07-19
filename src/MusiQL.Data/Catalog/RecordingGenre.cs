namespace MusiQL.Data.Catalog;

public class RecordingGenre
{
    public long RecordingId { get; set; }
    public long GenreId { get; set; }
    public int Votes { get; set; }

    public Recording? Recording { get; set; }
    public Genre? Genre { get; set; }
}
