namespace MusiQL.Data.App;

public class SpotifyAuthState
{
    public string State { get; set; } = "";
    public Guid UserId { get; set; }
    public string CodeVerifier { get; set; } = "";
    public string RedirectUri { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}
