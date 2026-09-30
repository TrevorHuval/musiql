using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using MusiQL.Data.App;

namespace MusiQL.Api.Spotify;

// The daily Spotify pass. It first re-syncs the saved tracks of every account
// that has synced its library before (so likes made in Spotify reach
// `from library` queries without a manual sync), then re-exports "keep live"
// playlists so the Spotify copy follows the query as the catalog and the
// library change. Runs one account or playlist at a time; a playlist failure is
// recorded on its link for the UI and retried after a back-off instead of on
// every pass, and a library sync failure is backed off the same way in memory.
public sealed class LivePlaylistRefresher(
    IServiceScopeFactory scopes, OperationLocks locks, ILogger<LivePlaylistRefresher> log, TimeProvider clock)
    : BackgroundService
{
    public static readonly TimeSpan RefreshEvery = TimeSpan.FromDays(1);
    public static readonly TimeSpan LibrarySyncEvery = TimeSpan.FromDays(1);
    public static readonly TimeSpan RetryFailedAfter = TimeSpan.FromHours(6);
    private static readonly TimeSpan PollEvery = TimeSpan.FromMinutes(30);
    private const int MaxErrorLength = 500;

    private readonly ConcurrentDictionary<Guid, DateTime> _librarySyncFailedAt = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish starting (and genre sizes load) before the first pass.
        await Task.Delay(TimeSpan.FromMinutes(1), clock, stoppingToken);
        using var timer = new PeriodicTimer(PollEvery, clock);
        do
        {
            try
            {
                await RunDueAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Live playlist refresh pass failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> RunDueAsync(CancellationToken ct)
    {
        await SyncLibrariesAsync(ct);

        var due = await DueAsync(ct);
        foreach (var playlistId in due)
        {
            await RefreshAsync(playlistId, ct);
        }

        return due.Count;
    }

    // Only accounts that already synced once are refreshed: the first sync is the
    // user's opt-in to keeping their saved tracks in MusiQL.
    public async Task<int> SyncLibrariesAsync(CancellationToken ct)
    {
        var due = await LibrarySyncDueAsync(ct);
        var synced = 0;
        foreach (var userId in due)
        {
            if (await SyncLibraryAsync(userId, ct))
            {
                synced++;
            }
        }

        return synced;
    }

    private async Task<List<Guid>> LibrarySyncDueAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = clock.GetUtcNow().UtcDateTime;
        var staleBefore = now - LibrarySyncEvery;
        var retryBefore = now - RetryFailedAfter;

        var stale = await db.SpotifyAccounts
            .Where(a => a.LibrarySyncedAt != null && a.LibrarySyncedAt < staleBefore)
            .OrderBy(a => a.LibrarySyncedAt)
            .Select(a => a.UserId)
            .ToListAsync(ct);

        return stale
            .Where(id => !_librarySyncFailedAt.TryGetValue(id, out var failedAt) || failedAt < retryBefore)
            .ToList();
    }

    private async Task<bool> SyncLibraryAsync(Guid userId, CancellationToken ct)
    {
        IDisposable held;
        try
        {
            held = locks.Acquire($"sync:{userId}", "A library sync");
        }
        catch (OperationInProgressException)
        {
            return false;
        }

        using (held)
        {
            using var scope = scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<SpotifyLibraryService>();
            try
            {
                var result = await library.SyncAsync(userId, ct);
                _librarySyncFailedAt.TryRemove(userId, out _);
                log.LogInformation("Synced Spotify library for {UserId}: {Matched}/{Saved} saved tracks matched",
                    userId, result.MatchedCount, result.SavedCount);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _librarySyncFailedAt[userId] = clock.GetUtcNow().UtcDateTime;
                log.LogWarning(ex, "Spotify library sync for {UserId} failed", userId);
                return false;
            }
        }
    }

    private async Task<List<Guid>> DueAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = clock.GetUtcNow().UtcDateTime;
        var staleBefore = now - RefreshEvery;
        var retryBefore = now - RetryFailedAfter;

        return await db.SpotifyPlaylistLinks
            .Where(l => l.KeepLive
                && l.LastExportedAt < staleBefore
                && (l.LastRefreshError == null || l.LastRefreshAttemptAt == null || l.LastRefreshAttemptAt < retryBefore))
            .OrderBy(l => l.LastExportedAt)
            .Select(l => l.PlaylistId)
            .ToListAsync(ct);
    }

    private async Task RefreshAsync(Guid playlistId, CancellationToken ct)
    {
        IDisposable held;
        try
        {
            held = locks.Acquire($"export:{playlistId}", "An export of this playlist");
        }
        catch (OperationInProgressException)
        {
            return;
        }

        using (held)
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var export = scope.ServiceProvider.GetRequiredService<SpotifyExportService>();

            var playlist = await db.Playlists.FirstOrDefaultAsync(p => p.Id == playlistId, ct);
            if (playlist is null)
            {
                return;
            }

            string? error = null;
            try
            {
                var result = await export.ExportAsync(playlist, rematch: false, ct);
                log.LogInformation("Refreshed live playlist {PlaylistId}: {Matched}/{Total} tracks",
                    playlistId, result.MatchedCount, result.TotalCount);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                error = ex switch
                {
                    SpotifyNotConnectedException => "Spotify is no longer connected. Reconnect it in settings.",
                    SpotifyNotConfiguredException => "Spotify integration is not configured on the server.",
                    ExportNotSupportedException e => e.Message,
                    SpotifyApiException e => $"Spotify rejected the update: {e.Message}",
                    _ => "The refresh failed. It will be retried."
                };
                log.LogWarning(ex, "Live playlist {PlaylistId} refresh failed", playlistId);
            }

            // Record the outcome on a fresh read: the export saved its own changes.
            db.ChangeTracker.Clear();
            var link = await db.SpotifyPlaylistLinks.FirstOrDefaultAsync(l => l.PlaylistId == playlistId, ct);
            if (link is not null)
            {
                link.LastRefreshAttemptAt = clock.GetUtcNow().UtcDateTime;
                link.LastRefreshError = error?[..Math.Min(MaxErrorLength, error.Length)];
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
