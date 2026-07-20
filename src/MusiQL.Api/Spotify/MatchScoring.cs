namespace MusiQL.Api.Spotify;

public static class MatchScoring
{
    public const double AcceptThreshold = 0.62;

    private const double TitleWeight = 0.5;
    private const double ArtistWeight = 0.35;
    private const double DurationWeight = 0.15;
    private const double DurationToleranceMs = 15000;

    public static double Score(RecordingRef recording, SpotifyTrack candidate)
    {
        var title = TextSimilarity.Ratio(recording.Title, candidate.Name);
        var artist = BestArtist(recording.Artist, candidate.Artists);
        var duration = DurationScore(recording.DurationMs, candidate.DurationMs);
        return TitleWeight * title + ArtistWeight * artist + DurationWeight * duration;
    }

    private static double BestArtist(string artist, IReadOnlyList<string> candidates)
    {
        if (candidates.Count == 0)
        {
            return 0;
        }

        var best = 0.0;
        foreach (var candidate in candidates)
        {
            best = Math.Max(best, TextSimilarity.Ratio(artist, candidate));
        }

        return Math.Max(best, TextSimilarity.Ratio(artist, string.Join(" ", candidates)));
    }

    private static double DurationScore(int? a, int? b)
    {
        if (a is null || b is null)
        {
            return 0.6;
        }

        var diff = Math.Abs(a.Value - b.Value);
        return 1.0 - Math.Min(1.0, diff / DurationToleranceMs);
    }
}
