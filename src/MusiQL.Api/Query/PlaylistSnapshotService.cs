using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MusiQL.Api.Contracts;
using MusiQL.Core.Mql.Compilation;
using MusiQL.Data.App;

namespace MusiQL.Api.Query;

public sealed class PlaylistSnapshotService(AppDbContext db, QueryService query, IOptions<QueryOptions> options)
{
    private TimeSpan Ttl => options.Value.SnapshotTtl;

    public async Task<QueryPageResponse> GetPageAsync(
        Playlist playlist, CompiledQuery compiled, int page, int pageSize, CancellationToken ct)
    {
        if (compiled.FromLibrary || Ttl <= TimeSpan.Zero)
        {
            return await query.RunAsync(compiled, playlist.OwnerId, page, pageSize, ct);
        }

        var snapshot = await db.PlaylistSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(s => s.PlaylistId == playlist.Id, ct);
        if (IsFresh(snapshot, playlist))
        {
            var stored = JsonSerializer.Deserialize<StoredSnapshot>(snapshot!.Payload)
                ?? throw new InvalidOperationException("Corrupt playlist snapshot.");
            return QueryService.Paginate(stored.Entity, stored.Columns, stored.Rows, page, pageSize, null);
        }

        var result = await query.ExecuteAsync(compiled, ct);
        var columns = QueryService.Columns(result);
        await StoreAsync(playlist, compiled.Entity, columns, result.Rows, ct);
        return QueryService.Paginate(compiled.Entity, columns, result.Rows, page, pageSize, null);
    }

    private bool IsFresh(PlaylistSnapshot? snapshot, Playlist playlist) =>
        snapshot is not null
        && snapshot.MqlText == playlist.MqlText
        && DateTime.UtcNow - snapshot.ComputedAt < Ttl;

    private async Task StoreAsync(
        Playlist playlist,
        string entity,
        IReadOnlyList<QueryColumn> columns,
        IReadOnlyList<IReadOnlyList<object?>> rows,
        CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new StoredSnapshot(entity, columns, rows));
        var existing = await db.PlaylistSnapshots.FirstOrDefaultAsync(s => s.PlaylistId == playlist.Id, ct);
        if (existing is null)
        {
            db.PlaylistSnapshots.Add(new PlaylistSnapshot
            {
                PlaylistId = playlist.Id,
                MqlText = playlist.MqlText,
                ComputedAt = DateTime.UtcNow,
                Payload = payload
            });
        }
        else
        {
            existing.MqlText = playlist.MqlText;
            existing.ComputedAt = DateTime.UtcNow;
            existing.Payload = payload;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
        }
    }

    private sealed record StoredSnapshot(
        string Entity,
        IReadOnlyList<QueryColumn> Columns,
        IReadOnlyList<IReadOnlyList<object?>> Rows);
}
