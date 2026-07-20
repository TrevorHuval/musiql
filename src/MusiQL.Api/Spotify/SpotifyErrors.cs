namespace MusiQL.Api.Spotify;

public sealed class SpotifyNotConfiguredException() : Exception("Spotify integration is not configured.");

public sealed class SpotifyNotConnectedException() : Exception("This account is not connected to Spotify.");

public sealed class SpotifyAuthStateException() : Exception("The Spotify authorization request is invalid or expired.");

public sealed class ExportNotSupportedException(string message) : Exception(message);
