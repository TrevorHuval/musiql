using MusiQL.Api.Spotify;

namespace MusiQL.Tests.Spotify;

public class AccessRequestFormattingTests
{
    [Theory]
    [InlineData("  person@example.com ", "person@example.com")]
    [InlineData("first.last+tag@sub.example.co.uk", "first.last+tag@sub.example.co.uk")]
    public void Accepts_bare_addresses(string input, string expected) =>
        Assert.Equal(expected, SpotifyAccessRequestService.NormalizeEmail(input));

    [Theory]
    [InlineData(null)]
    [InlineData("a@example.com\r\nBcc: b@example.com")]
    [InlineData("a@example.com\u0000")]
    [InlineData("\"Quoted\" <a@example.com>")]
    [InlineData("a@b")]
    public void Rejects_anything_else(string? input) =>
        Assert.Null(SpotifyAccessRequestService.NormalizeEmail(input));

    [Fact]
    public void Rejects_addresses_over_the_length_limit() =>
        Assert.Null(SpotifyAccessRequestService.NormalizeEmail(new string('a', 250) + "@example.com"));

    [Fact]
    public void Note_keeps_line_breaks_and_drops_other_control_characters()
    {
        var note = SpotifyAccessRequestService.NormalizeNote("hi\r\nthere\u0007\u001b[31m");
        Assert.Equal("hi\nthere[31m", note);
    }

    [Fact]
    public void Note_lines_are_quoted_so_they_cannot_pose_as_fields()
    {
        var body = SpotifyAccessRequestService.BuildBody(
            Guid.Empty, "account@example.com", "spotify@example.com",
            "hello\nSpotify account email: attacker@example.com", new DateTime(2026, 11, 16, 23, 0, 0));

        Assert.Contains("\nSpotify account email: spotify@example.com", body);
        Assert.Contains("> Spotify account email: attacker@example.com", body);
        Assert.DoesNotContain("\nSpotify account email: attacker@example.com", body);
    }
}
