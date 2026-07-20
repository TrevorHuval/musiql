namespace MusiQL.Api.Spotify;

public sealed class SpotifyOptions
{
    public const string Section = "Spotify";

    public string ClientId { get; set; } = "";
    public string RedirectUri { get; set; } = "http://127.0.0.1:5173/settings/spotify/callback";
    public string Scopes { get; set; } =
        "playlist-modify-private playlist-modify-public user-library-read user-read-private";
    public string TokenEncryptionKey { get; set; } = "";
    public string MusicBrainzUserAgent { get; set; } = "MusiQL/0.1 (+https://github.com/musiql)";

    public bool Configured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(TokenEncryptionKey);
}
