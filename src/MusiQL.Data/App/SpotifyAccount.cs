namespace MusiQL.Data.App;

public class SpotifyAccount
{
    public Guid UserId { get; set; }
    public string SpotifyUserId { get; set; } = "";
    public string? DisplayName { get; set; }
    public string AccessTokenCipher { get; set; } = "";
    public string RefreshTokenCipher { get; set; } = "";
    public string Scopes { get; set; } = "";
    public DateTime AccessExpiresAt { get; set; }
    public DateTime ConnectedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LibrarySyncedAt { get; set; }
    public int LibrarySavedCount { get; set; }
    public int LibraryMatchedCount { get; set; }
}
