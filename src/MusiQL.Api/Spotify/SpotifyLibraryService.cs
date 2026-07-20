using Microsoft.EntityFrameworkCore;
using MusiQL.Api.Contracts;
using MusiQL.Data;
using MusiQL.Data.App;

namespace MusiQL.Api.Spotify;

public sealed class SpotifyLibraryService(
    ISpotifyClientFactory clientFactory,
    CatalogMatcher catalogMatcher,
    AppDbContext db,
    MusiQLDbContext catalog)
{
    private const int PageSize = 50;

    public async Task<LibrarySyncResponse> SyncAsync(Guid userId, CancellationToken ct)
    {
        var session = await clientFactory.ForUserAsync(userId, ct);
        var saved = await FetchSavedAsync(session, ct);

        var now = DateTime.UtcNow;
        var currentIds = saved.Select(s => s.TrackId).ToHashSet();
        var existing = await db.SpotifySavedTracks.Where(t => t.UserId == userId).ToListAsync(ct);
        var byId = existing.ToDictionary(t => t.SpotifyTrackId);
        db.SpotifySavedTracks.RemoveRange(existing.Where(t => !currentIds.Contains(t.SpotifyTrackId)));

        var library = new Dictionary<long, DateTime>();
        var matchedCount = 0;
        foreach (var item in saved)
        {
            if (!byId.TryGetValue(item.TrackId, out var row))
            {
                row = new SpotifySavedTrack { UserId = userId, SpotifyTrackId = item.TrackId };
                db.SpotifySavedTracks.Add(row);
            }

            row.Isrc = item.Isrc;
            row.Title = item.Title;
            row.Artist = item.Artist;
            row.DurationMs = item.DurationMs;
            row.AddedAt = item.AddedAt;
            row.SyncedAt = now;

            if (row.RecordingId is null)
            {
                var match = await ResolveCatalogAsync(item, ct);
                if (match is not null)
                {
                    row.RecordingId = match.RecordingId;
                    row.RecordingMbid = match.Mbid;
                    row.Confidence = match.Confidence;
                }
            }

            if (row.RecordingId is { } recordingId)
            {
                matchedCount++;
                if (!library.TryGetValue(recordingId, out var addedAt) || item.AddedAt < addedAt)
                {
                    library[recordingId] = item.AddedAt;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        await ReconcileLibraryAsync(userId, library, ct);

        var account = await db.SpotifyAccounts.FirstAsync(a => a.UserId == userId, ct);
        account.LibrarySyncedAt = now;
        account.LibrarySavedCount = saved.Count;
        account.LibraryMatchedCount = matchedCount;
        await db.SaveChangesAsync(ct);

        return new LibrarySyncResponse(saved.Count, matchedCount, saved.Count - matchedCount, library.Count, now);
    }

    private static async Task<List<SpotifySavedItem>> FetchSavedAsync(SpotifyUserSession session, CancellationToken ct)
    {
        var saved = new List<SpotifySavedItem>();
        var offset = 0;
        while (true)
        {
            var page = await session.Client.GetSavedTracksAsync(offset, PageSize, ct);
            if (page.Items.Count == 0)
            {
                break;
            }

            saved.AddRange(page.Items);
            offset += page.Items.Count;
            if (offset >= page.Total)
            {
                break;
            }
        }

        return saved;
    }

    private async Task ReconcileLibraryAsync(Guid userId, Dictionary<long, DateTime> library, CancellationToken ct)
    {
        var existing = await catalog.UserLibrary.Where(l => l.UserId == userId).ToListAsync(ct);
        catalog.UserLibrary.RemoveRange(existing.Where(l => !library.ContainsKey(l.RecordingId)));
        var have = existing.Select(l => l.RecordingId).ToHashSet();

        foreach (var (recordingId, addedAt) in library)
        {
            if (!have.Contains(recordingId))
            {
                catalog.UserLibrary.Add(new UserLibrary
                {
                    UserId = userId,
                    RecordingId = recordingId,
                    AddedAt = addedAt
                });
            }
        }

        await catalog.SaveChangesAsync(ct);
    }

    private async Task<CatalogMatch?> ResolveCatalogAsync(SpotifySavedItem item, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(item.Isrc))
        {
            var byIsrc = await ResolveByIsrcAsync(item.Isrc, ct);
            if (byIsrc is not null)
            {
                return byIsrc;
            }
        }

        return await catalogMatcher.MatchAsync(item.Title, item.Artist, item.DurationMs, ct);
    }

    private async Task<CatalogMatch?> ResolveByIsrcAsync(string isrc, CancellationToken ct)
    {
        var mbid = await db.TrackMatches
            .Where(m => m.Isrc == isrc && m.SpotifyTrackId != null)
            .Select(m => (Guid?)m.RecordingMbid)
            .FirstOrDefaultAsync(ct);

        mbid ??= await db.RecordingIsrcs
            .Where(r => r.Isrc != null && (r.Isrc == isrc || r.Isrc.Contains(isrc)))
            .Select(r => (Guid?)r.RecordingMbid)
            .FirstOrDefaultAsync(ct);

        if (mbid is null)
        {
            return null;
        }

        var recording = await catalog.Recordings
            .Where(r => r.Mbid == mbid.Value)
            .Select(r => new { r.Id, r.Mbid })
            .FirstOrDefaultAsync(ct);

        return recording is null ? null : new CatalogMatch(recording.Id, recording.Mbid, 1.0);
    }
}
