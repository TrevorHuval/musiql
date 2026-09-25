using System.Net;
using System.Net.Http.Json;
using Npgsql;
using NpgsqlTypes;

namespace MusiQL.Etl.Popularity;

// Pulls listener counts from the ListenBrainz popularity API into the
// catalog.*_popularity tables. The API takes 1,000 MBIDs per request and allows
// roughly 30 requests per 10 seconds, so a full catalog is a multi-hour job:
// progress is committed per round and a rerun resumes after the last id.
public sealed class PopularityFetcher(string connectionString, Action<string> log, int parallel = 3)
{
    public const string DefaultBaseUrl = "https://api.listenbrainz.org/";
    private const int BatchSize = 1000;

    private static readonly Target[] Targets =
    [
        new("artist", "catalog.artist", "catalog.artist_popularity", "artist_id", "1/popularity/artist", "artist_mbids", "artist_mbid"),
        new("release_group", "catalog.release_group", "catalog.release_group_popularity", "release_group_id", "1/popularity/release-group", "release_group_mbids", "release_group_mbid"),
        new("recording", "catalog.recording", "catalog.recording_popularity", "recording_id", "1/popularity/recording", "recording_mbids", "recording_mbid")
    ];

    public async Task FetchAsync(IReadOnlyCollection<string>? only, bool restart, string baseUrl, CancellationToken ct)
    {
        using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromMinutes(2) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("MusiQL/0.1 (+https://github.com/TrevorHuval/musiql)");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        foreach (var target in Targets.Where(t => only is null || only.Count == 0 || only.Contains(t.Entity)))
        {
            await FetchTargetAsync(http, connection, target, restart, ct);
        }
    }

    private async Task FetchTargetAsync(
        HttpClient http, NpgsqlConnection connection, Target target, bool restart, CancellationToken ct)
    {
        var lastId = restart ? 0 : await LastIdAsync(connection, target, ct);
        var total = await ScalarAsync<long>(connection, $"SELECT count(*) FROM {target.Table} WHERE id > {lastId}", ct);
        log($"{target.Entity}: {total:N0} rows to fetch, resuming after id {lastId}");

        long done = 0, stored = 0;
        var started = DateTime.UtcNow;
        while (true)
        {
            var rows = await NextRowsAsync(connection, target, lastId, BatchSize * parallel, ct);
            if (rows.Count == 0)
            {
                break;
            }

            var batches = rows.Chunk(BatchSize).ToList();
            var results = await Task.WhenAll(batches.Select(b => FetchBatchAsync(http, target, b, ct)));

            var found = results.SelectMany(r => r).ToList();
            await StoreAsync(connection, target, found, rows[^1].Id, ct);

            lastId = rows[^1].Id;
            done += rows.Count;
            stored += found.Count;
            if (done % 100_000 < rows.Count || done == total)
            {
                var rate = done / Math.Max(1, (DateTime.UtcNow - started).TotalSeconds);
                var eta = TimeSpan.FromSeconds((total - done) / Math.Max(1, rate));
                log($"{target.Entity}: {done:N0}/{total:N0} ({stored:N0} with listeners), ~{eta:hh\\:mm} left");
            }
        }

        log($"{target.Entity}: done, {stored:N0} rows with listeners");
    }

    private async Task<IReadOnlyList<Popularity>> FetchBatchAsync(
        HttpClient http, Target target, Row[] batch, CancellationToken ct)
    {
        var byMbid = batch.ToDictionary(r => r.Mbid);
        var body = new Dictionary<string, Guid[]> { [target.RequestField] = batch.Select(r => r.Mbid).ToArray() };

        for (var attempt = 1; ; attempt++)
        {
            // Buffered body with a Content-Length: the API's server rejects the
            // chunked encoding PostAsJsonAsync streams by default.
            using var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(target.Path, content, ct);
            if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
            {
                if (attempt >= 8)
                {
                    response.EnsureSuccessStatusCode();
                }

                await Task.Delay(ResetDelay(response) ?? TimeSpan.FromSeconds(Math.Min(60, 2 << attempt)), ct);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException(
                    $"{target.Path} returned {(int)response.StatusCode}: {detail[..Math.Min(300, detail.Length)]}");
            }

            await PaceAsync(response, ct);

            var items = await response.Content.ReadFromJsonAsync<List<Dictionary<string, object?>>>(ct) ?? [];
            var results = new List<Popularity>(items.Count);
            foreach (var item in items)
            {
                var listeners = Number(item.GetValueOrDefault("total_user_count"));
                if (listeners is not > 0
                    || item.GetValueOrDefault(target.ResponseField)?.ToString() is not { } mbidText
                    || !Guid.TryParse(mbidText, out var mbid)
                    || !byMbid.TryGetValue(mbid, out var row))
                {
                    continue;
                }

                results.Add(new Popularity(row.Id, (int)Math.Min(int.MaxValue, listeners.Value),
                    Number(item.GetValueOrDefault("total_listen_count")) ?? 0));
            }

            return results;
        }
    }

    // Stay under the published limit: when the window is nearly spent, wait for
    // it to reset rather than collecting 429s.
    private static async Task PaceAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (Header(response, "X-RateLimit-Remaining") is { } remaining && remaining <= 3
            && ResetDelay(response) is { } delay)
        {
            await Task.Delay(delay, ct);
        }
    }

    private static TimeSpan? ResetDelay(HttpResponseMessage response) =>
        Header(response, "X-RateLimit-Reset-In") is { } seconds ? TimeSpan.FromSeconds(seconds + 1) : null;

    private static long? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) && long.TryParse(values.FirstOrDefault(), out var value)
            ? value
            : null;

    private static long? Number(object? value) => value switch
    {
        null => null,
        System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Number } e => e.GetInt64(),
        _ => long.TryParse(value.ToString(), out var n) ? n : null
    };

    private static async Task<List<Row>> NextRowsAsync(
        NpgsqlConnection connection, Target target, long afterId, int count, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            $"SELECT id, mbid FROM {target.Table} WHERE id > @after ORDER BY id LIMIT @count", connection);
        command.Parameters.AddWithValue("after", afterId);
        command.Parameters.AddWithValue("count", count);

        var rows = new List<Row>(count);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new Row(reader.GetInt64(0), reader.GetGuid(1)));
        }

        return rows;
    }

    private static async Task StoreAsync(
        NpgsqlConnection connection, Target target, IReadOnlyList<Popularity> found, long lastId, CancellationToken ct)
    {
        await using var transaction = await connection.BeginTransactionAsync(ct);
        if (found.Count > 0)
        {
            await using var command = new NpgsqlCommand($"""
                INSERT INTO {target.PopularityTable} ({target.KeyColumn}, listeners, listens)
                SELECT * FROM unnest(@ids, @listeners, @listens)
                ON CONFLICT ({target.KeyColumn}) DO UPDATE
                    SET listeners = excluded.listeners, listens = excluded.listens
                """, connection, transaction);
            command.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint)
                { Value = found.Select(f => f.Id).ToArray() });
            command.Parameters.Add(new NpgsqlParameter("listeners", NpgsqlDbType.Array | NpgsqlDbType.Integer)
                { Value = found.Select(f => f.Listeners).ToArray() });
            command.Parameters.Add(new NpgsqlParameter("listens", NpgsqlDbType.Array | NpgsqlDbType.Bigint)
                { Value = found.Select(f => f.Listens).ToArray() });
            await command.ExecuteNonQueryAsync(ct);
        }

        await using (var progress = new NpgsqlCommand("""
            INSERT INTO catalog.popularity_progress (entity, last_id, updated_at) VALUES (@entity, @last, now())
            ON CONFLICT (entity) DO UPDATE SET last_id = excluded.last_id, updated_at = now()
            """, connection, transaction))
        {
            progress.Parameters.AddWithValue("entity", target.Entity);
            progress.Parameters.AddWithValue("last", lastId);
            await progress.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }

    private static async Task<long> LastIdAsync(NpgsqlConnection connection, Target target, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            "SELECT last_id FROM catalog.popularity_progress WHERE entity = @entity", connection);
        command.Parameters.AddWithValue("entity", target.Entity);
        return await command.ExecuteScalarAsync(ct) is long id ? id : 0;
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync(ct))!;
    }

    private sealed record Target(
        string Entity, string Table, string PopularityTable, string KeyColumn,
        string Path, string RequestField, string ResponseField);

    private sealed record Row(long Id, Guid Mbid);

    private sealed record Popularity(long Id, int Listeners, long Listens);
}
