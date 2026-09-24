using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MusiQL.Data;

namespace MusiQL.Api.Query;

// Knows which genres are small enough for the compiler's id-probe shape. The
// catalog only changes on a refresh, so counts are loaded at startup and
// hourly; until the first load every genre compiles the conservative way.
public sealed class GenreSelectivity(
    IServiceScopeFactory scopes, IOptions<QueryOptions> options, ILogger<GenreSelectivity> log)
    : BackgroundService
{
    private static readonly TimeSpan Refresh = TimeSpan.FromHours(1);

    private volatile IReadOnlySet<string> _selective = new HashSet<string>();

    public IReadOnlySet<string> Selective => _selective;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Refresh);
        do
        {
            try
            {
                _selective = await LoadAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Could not load genre sizes; keeping the previous set");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<IReadOnlySet<string>> LoadAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<MusiQLDbContext>();
        var threshold = options.Value.SelectiveGenreLinks;

        var names = await catalog.Database.SqlQuery<string>($@"
            SELECT lower(g.name) AS ""Value""
            FROM catalog.genre g
            LEFT JOIN (
                SELECT genre_id, count(*) AS n FROM (
                    SELECT genre_id FROM catalog.recording_genre
                    UNION ALL SELECT genre_id FROM catalog.release_group_genre
                    UNION ALL SELECT genre_id FROM catalog.artist_genre) links
                GROUP BY genre_id) c ON c.genre_id = g.id
            WHERE coalesce(c.n, 0) < {threshold}")
            .ToListAsync(ct);

        return names.ToHashSet(StringComparer.Ordinal);
    }
}
