using Microsoft.EntityFrameworkCore;
using MusiQL.Data;

namespace MusiQL.Api.Spotify;

public sealed class CatalogMatcher(MusiQLDbContext catalog)
{
    private const double TitleAcceptThreshold = 0.72;
    private const double DurationToleranceMs = 15000;
    private const int CandidateLimit = 200;

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

    private sealed record Candidate(long Id, Guid Mbid, string Name, int? LengthMs);
}
