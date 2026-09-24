using Microsoft.EntityFrameworkCore;
using MusiQL.Data;

namespace MusiQL.Api.Spotify;

public sealed class CatalogMatcher(MusiQLDbContext catalog)
{
    private const double TitleAcceptThreshold = 0.72;
    private const double DurationToleranceMs = 15000;
    private const int CandidateLimit = 200;

    // One round trip for a whole library: every track whose primary artist and
    // title match a catalog recording exactly (case-insensitive), keyed by the
    // track's index in the input. Closest duration wins when several match.
    public async Task<IReadOnlyDictionary<int, CatalogMatch>> MatchExactAsync(
        IReadOnlyList<(string Title, string Artist, int? DurationMs)> tracks, CancellationToken ct)
    {
        if (tracks.Count == 0)
        {
            return new Dictionary<int, CatalogMatch>();
        }

        var artists = tracks.Select(t => PrimaryArtist(t.Artist).ToLowerInvariant()).ToArray();
        var titles = tracks.Select(t => TextSimilarity.TitleCore(t.Title).ToLowerInvariant()).ToArray();
        var durations = tracks.Select(t => t.DurationMs ?? -1).ToArray();

        var hits = await catalog.Database.SqlQuery<ExactHit>($@"
            SELECT DISTINCT ON (q.ord) q.ord::int AS ""Index"", r.id AS ""Id"", r.mbid AS ""Mbid""
            FROM unnest({artists}::text[], {titles}::text[], {durations}::int[])
                 WITH ORDINALITY AS q(artist, title, duration, ord)
            JOIN catalog.artist a ON lower(a.name) = q.artist
            JOIN catalog.recording r ON r.artist_id = a.id AND lower(r.name) = q.title
            ORDER BY q.ord, abs(coalesce(r.length_ms, 0) - greatest(q.duration, 0))")
            .ToListAsync(ct);

        return hits.ToDictionary(h => h.Index - 1, h => new CatalogMatch(h.Id, h.Mbid, 1.0));
    }

    public async Task<CatalogMatch?> MatchAsync(string title, string artist, int? durationMs, CancellationToken ct)
    {
        var primary = PrimaryArtist(artist);
        if (primary.Length == 0)
        {
            return null;
        }

        var lowered = primary.ToLowerInvariant();
        var candidates = await catalog.Recordings
            .Where(r => r.Artist!.Name.ToLower() == lowered)
            .Select(r => new Candidate(r.Id, r.Mbid, r.Name, r.LengthMs))
            .Take(CandidateLimit)
            .ToListAsync(ct);

        Candidate? best = null;
        var bestScore = 0.0;
        var bestTitle = 0.0;
        foreach (var candidate in candidates)
        {
            var titleScore = TextSimilarity.Ratio(title, candidate.Name);
            var score = 0.75 * titleScore + 0.25 * DurationScore(durationMs, candidate.LengthMs);
            if (best is null || score > bestScore)
            {
                best = candidate;
                bestScore = score;
                bestTitle = titleScore;
            }
        }

        if (best is null || bestTitle < TitleAcceptThreshold)
        {
            return null;
        }

        return new CatalogMatch(best.Id, best.Mbid, Math.Round(bestScore, 4));
    }

    private static string PrimaryArtist(string artist)
    {
        var comma = artist.IndexOf(',');
        return (comma >= 0 ? artist[..comma] : artist).Trim();
    }

    private static double DurationScore(int? a, int? b)
    {
        if (a is null || b is null)
        {
            return 0.6;
        }

        return 1.0 - Math.Min(1.0, Math.Abs(a.Value - b.Value) / DurationToleranceMs);
    }

    private sealed record ExactHit(int Index, long Id, Guid Mbid);

    private sealed record Candidate(long Id, Guid Mbid, string Name, int? LengthMs);
}
