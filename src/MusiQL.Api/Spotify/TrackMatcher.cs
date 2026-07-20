namespace MusiQL.Api.Spotify;

public sealed class TrackMatcher(IMusicBrainzIsrcLookup isrcLookup)
{
    public async Task<TrackMatchResult> MatchAsync(
        RecordingRef recording, ISpotifySearch search, CancellationToken ct)
    {
        var isrcs = await isrcLookup.GetIsrcsAsync(recording.Mbid, ct);
        foreach (var isrc in isrcs)
        {
            var hits = await search.SearchByIsrcAsync(isrc, ct);
            var byIsrc = BestByScore(recording, hits);
            if (byIsrc is not null)
            {
                return new TrackMatchResult(byIsrc.Track.Id, byIsrc.Track.Uri, 1.0, MatchMethod.Isrc, isrc);
            }
        }

        var results = await search.SearchByTextAsync(recording.Title, recording.Artist, ct);
        var best = BestByScore(recording, results);
        if (best is not null && best.Score >= MatchScoring.AcceptThreshold)
        {
            return new TrackMatchResult(
                best.Track.Id, best.Track.Uri, Round(best.Score), MatchMethod.Search, best.Track.Isrc);
        }

        return TrackMatchResult.Unmatched(best is null ? 0 : Round(best.Score));
    }

    private static Scored? BestByScore(RecordingRef recording, IReadOnlyList<SpotifyTrack> tracks)
    {
        Scored? best = null;
        foreach (var track in tracks)
        {
            var score = MatchScoring.Score(recording, track);
            if (best is null || score > best.Score)
            {
                best = new Scored(track, score);
            }
        }

        return best;
    }

    private static double Round(double value) => Math.Round(value, 4);

    private sealed record Scored(SpotifyTrack Track, double Score);
}
