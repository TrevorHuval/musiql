using Microsoft.EntityFrameworkCore;
using MusiQL.Data.App;

namespace MusiQL.Api.Spotify;

// Re-exports "keep live" playlists to Spotify once a day so the Spotify copy
// follows the query as the catalog and the user's library change. Runs one
// playlist at a time; a failure is recorded on the link for the UI and retried
// after a back-off instead of on every pass.
public sealed class LivePlaylistRefresher(
    IServiceScopeFactory scopes, OperationLocks locks, ILogger<LivePlaylistRefresher> log, TimeProvider clock)
    : BackgroundService
{
    public static readonly TimeSpan RefreshEvery = TimeSpan.FromDays(1);
    public static readonly TimeSpan RetryFailedAfter = TimeSpan.FromHours(6);
    private static readonly TimeSpan PollEvery = TimeSpan.FromMinutes(30);
    private const int MaxErrorLength = 500;

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
        var due = await DueAsync(ct);
        foreach (var playlistId in due)
        {
            await RefreshAsync(playlistId, ct);
        }

        return due.Count;
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
