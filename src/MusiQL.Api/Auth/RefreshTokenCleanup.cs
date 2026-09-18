using Microsoft.EntityFrameworkCore;
using MusiQL.Data.App;

namespace MusiQL.Api.Auth;

// Deletes refresh tokens that have expired or been revoked for longer than the
// grace window, in small batches so no single statement holds locks for long.
public sealed class RefreshTokenCleanup(IServiceScopeFactory scopes, ILogger<RefreshTokenCleanup> log)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private static readonly TimeSpan RevokedGrace = TimeSpan.FromDays(1);
    private const int Batch = 1000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                var removed = await SweepAsync(stoppingToken);
                if (removed > 0)
                {
                    log.LogInformation("Removed {Count} stale refresh tokens", removed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Refresh token cleanup failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> SweepAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var revokedBefore = now - RevokedGrace;

        var total = 0;
        int removed;
        do
        {
            var ids = await db.RefreshTokens
                .Where(t => t.ExpiresAt <= now || t.RevokedAt < revokedBefore)
                .OrderBy(t => t.Id)
                .Take(Batch)
                .Select(t => t.Id)
                .ToListAsync(ct);

            removed = ids.Count == 0
                ? 0
                : await db.RefreshTokens.Where(t => ids.Contains(t.Id)).ExecuteDeleteAsync(ct);
            total += removed;
        }
        while (removed == Batch);

        return total;
    }
}
