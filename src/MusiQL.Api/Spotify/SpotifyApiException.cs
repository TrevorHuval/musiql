namespace MusiQL.Api.Spotify;

public sealed class SpotifyApiException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
