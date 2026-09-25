using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MusiQL.Data;

namespace MusiQL.Api.Spotify;

public sealed class CatalogMatcher(MusiQLDbContext catalog)
{
    private const double TitleAcceptThreshold = 0.72;
    private const double DurationToleranceMs = 15000;
    private const int CandidateLimit = 25;
    private const double MinimumScore = 0.6;

    private static readonly Regex VersionMarker = new(
        @"(karaoke|remix(ed)?|instrumental|live|demo|video|mixed|acoustic|cover|tribute|rehearsal|session|originally performed)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

    // Fallback for tracks the exact pass missed: the artist's recordings whose
    // titles are closest by trigram similarity, scored on title and duration.
    // A near title only counts when the lengths agree; otherwise "It's Your
    // Love" happily becomes "It's Your World".
    public async Task<CatalogMatch?> MatchAsync(string title, string artist, int? durationMs, CancellationToken ct)
    {
        var primary = PrimaryArtist(artist).ToLowerInvariant();
        var core = TextSimilarity.TitleCore(title).ToLowerInvariant();
        if (primary.Length == 0 || core.Length == 0)
        {
            return null;
        }

        var candidates = await catalog.Database.SqlQuery<Candidate>($@"
            SELECT r.id AS ""Id"", r.mbid AS ""Mbid"", r.name AS ""Name"", r.length_ms AS ""LengthMs""
            FROM catalog.artist a
            JOIN catalog.recording r ON r.artist_id = a.id
            WHERE lower(a.name) = {primary}
            ORDER BY similarity(lower(r.name), {core}) DESC, r.id
            LIMIT {CandidateLimit}")
            .ToListAsync(ct);

        Candidate? best = null;
        var bestScore = 0.0;
        foreach (var candidate in candidates)
        {
            var titleScore = TextSimilarity.Ratio(title, candidate.Name);
            var durationScore = DurationScore(durationMs, candidate.LengthMs);
            if (titleScore < TitleAcceptThreshold || (titleScore < 1.0 && durationScore == 0.0))
            {
                continue;
            }

            var score = 0.75 * titleScore + 0.25 * durationScore - VersionPenalty(title, candidate.Name);
            if (best is null || score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best is null || bestScore < MinimumScore
            ? null
            : new CatalogMatch(best.Id, best.Mbid, Math.Round(bestScore, 4));
    }

    // Karaoke, remix, live and similar versions share the song's title but are
    // not the recording someone saved, unless their own title says so too.
    private static double VersionPenalty(string savedTitle, string candidateName)
    {
        var saved = VersionMarker.Matches(savedTitle).Select(m => m.Value.ToLowerInvariant()).ToHashSet();
        return VersionMarker.Matches(candidateName).Any(m => !saved.Contains(m.Value.ToLowerInvariant())) ? 0.3 : 0.0;
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
