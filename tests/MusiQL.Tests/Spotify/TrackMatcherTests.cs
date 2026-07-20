using MusiQL.Api.Spotify;
using Xunit;

namespace MusiQL.Tests.Spotify;

public class TrackMatcherTests
{
    private static SpotifyTrack Track(
        string id, string name, string artist, int? duration = null, string? isrc = null) =>
        new(id, $"spotify:track:{id}", name, [artist], duration, isrc);

    [Fact]
    public async Task Isrc_match_wins_with_full_confidence()
    {
        var mbid = Guid.NewGuid();
        var isrc = new FakeIsrcLookup();
        isrc.Map[mbid] = ["GBAYE0000123"];

        var client = new FakeSpotifyClient
        {
            IsrcSearch = value => [Track("spotify-1", "Anything At All", "Whoever", 200000, value)]
        };

        var matcher = new TrackMatcher(isrc);
        var result = await matcher.MatchAsync(
            new RecordingRef(mbid, "Come as You Are", "Nirvana", 219000), client, default);

        Assert.True(result.Matched);
        Assert.Equal(MatchMethod.Isrc, result.Method);
        Assert.Equal("spotify-1", result.SpotifyTrackId);
        Assert.Equal(1.0, result.Confidence);
        Assert.Equal("GBAYE0000123", result.Isrc);
        Assert.Equal(0, client.TextSearchCalls);
    }

    [Fact]
    public async Task Falls_back_to_text_search_when_no_isrc()
    {
        var client = new FakeSpotifyClient
        {
            TextSearch = (title, artist) => [Track("spotify-2", title, artist, 301000, "USABC1234567")]
        };

        var matcher = new TrackMatcher(new FakeIsrcLookup());
        var result = await matcher.MatchAsync(
            new RecordingRef(Guid.NewGuid(), "Smells Like Teen Spirit", "Nirvana", 301000), client, default);

        Assert.True(result.Matched);
        Assert.Equal(MatchMethod.Search, result.Method);
        Assert.Equal("spotify-2", result.SpotifyTrackId);
        Assert.True(result.Confidence >= MatchScoring.AcceptThreshold);
    }

    [Fact]
    public async Task Rejects_poor_candidates_as_unmatched()
    {
        var client = new FakeSpotifyClient
        {
            TextSearch = (_, _) => [Track("spotify-3", "Totally Different Song", "Some Other Band", 120000)]
        };

        var matcher = new TrackMatcher(new FakeIsrcLookup());
        var result = await matcher.MatchAsync(
            new RecordingRef(Guid.NewGuid(), "Lithium", "Nirvana", 257000), client, default);

        Assert.False(result.Matched);
        Assert.Equal(MatchMethod.None, result.Method);
        Assert.Null(result.SpotifyTrackId);
    }

    [Fact]
    public async Task Prefers_candidate_closest_in_duration()
    {
        var client = new FakeSpotifyClient
        {
            TextSearch = (title, artist) =>
            [
                Track("short", title, artist, 180000),
                Track("exact", title, artist, 300000)
            ]
        };

        var matcher = new TrackMatcher(new FakeIsrcLookup());
        var result = await matcher.MatchAsync(
            new RecordingRef(Guid.NewGuid(), "Black", "Pearl Jam", 300000), client, default);

        Assert.Equal("exact", result.SpotifyTrackId);
    }
}
