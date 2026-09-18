namespace MusiQL.Data.App;

public class Playlist
{
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 2000;
    public const int MaxMqlLength = 8000;

    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string MqlText { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
