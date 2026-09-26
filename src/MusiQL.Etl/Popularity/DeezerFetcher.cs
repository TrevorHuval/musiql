using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;

namespace MusiQL.Etl.Popularity;

// Current popularity from Deezer for the catalog's best-known artists. For each
// artist, in ListenBrainz order, find it on Deezer by exact name (most fans
// wins), take its top tracks with their rank, and map each title onto the
// artist's recording of that title with the most ListenBrainz listeners. Ranks
// land in catalog.recording_deezer; blend.sql folds them into the score.
//
// Deezer allows about 50 requests per 5 seconds; requests go through one
// shared pacer. Progress is the number of artists done, so a rerun resumes.
public sealed partial class DeezerFetcher(string connectionString, Action<string> log, int parallel = 4)
{
    public const string DefaultBaseUrl = "https://api.deezer.com/";
    private const string ProgressKey = "deezer";
    private const int TopTracks = 100;
    private static readonly TimeSpan RequestSpacing = TimeSpan.FromMilliseconds(115);

    private readonly SemaphoreSlim _pace = new(1, 1);
    private DateTime _nextRequest = DateTime.MinValue;

    public async Task FetchAsync(int artistCount, bool restart, string baseUrl, CancellationToken ct)
    {
        using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("MusiQL/0.1 (+https://github.com/TrevorHuval/musiql)");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        var done = restart ? 0 : await ProgressAsync(connection, ct);
        var artists = await ArtistsAsync(connection, artistCount, ct);
        log($"deezer: {artists.Count - done:N0} of {artists.Count:N0} artists to go");

        var started = DateTime.UtcNow;
        var mapped = 0L;
        for (var offset = done; offset < artists.Count; offset += parallel * 25)
        {
            var round = artists.Skip(offset).Take(parallel * 25).ToList();
            var results = new List<(long RecordingId, int Rank, long Fans)>();
            await Parallel.ForEachAsync(round, new ParallelOptions { MaxDegreeOfParallelism = parallel, CancellationToken = ct },
                async (artist, token) =>
                {
                    var (tracks, fans) = await TopTracksAsync(http, artist.Name, token);
                    if (tracks.Count == 0)
                    {
                        return;
                    }

                    await using var lookup = new NpgsqlConnection(connectionString);
                    await lookup.OpenAsync(token);
                    var matches = await MatchAsync(lookup, artist.Id, tracks, token);
                    lock (results)
                    {
                        results.AddRange(matches.Select(m => (m.RecordingId, m.Rank, fans)));
                    }
                });

            await StoreAsync(connection, results, offset + round.Count, ct);
            mapped += results.Count;

            var processed = offset + round.Count - done;
            if (processed % 2000 < round.Count || offset + round.Count >= artists.Count)
            {
                var rate = processed / Math.Max(1, (DateTime.UtcNow - started).TotalSeconds);
                var eta = TimeSpan.FromSeconds((artists.Count - offset - round.Count) / Math.Max(0.01, rate));
                log($"deezer: {offset + round.Count:N0}/{artists.Count:N0} artists, {mapped:N0} tracks ranked, ~{eta:hh\\:mm} left");
            }
        }

        log($"deezer: done, {mapped:N0} tracks ranked this run");
    }

    private async Task<(List<(string Title, int Rank)> Tracks, long Fans)> TopTracksAsync(HttpClient http, string artistName, CancellationToken ct)
    {
        // A plain name query: Deezer's artist:"..." syntax leaves out the most
        // popular exact match (no Queen for "Queen"), so search broadly and pick
        // the exact name with the most fans.
        var search = await GetAsync(http, $"search/artist?q={Uri.EscapeDataString(artistName)}&limit=25", ct);
        if (search is null || !search.Value.TryGetProperty("data", out var candidates))
        {
            return ([], 0);
        }

        var wanted = Normalize(artistName);
        long? deezerId = null;
        long bestFans = -1;
        foreach (var candidate in candidates.EnumerateArray())
        {
            var name = candidate.GetProperty("name").GetString() ?? "";
            var fans = candidate.TryGetProperty("nb_fan", out var f) ? f.GetInt64() : 0;
            if (Normalize(name) == wanted && fans > bestFans)
            {
                deezerId = candidate.GetProperty("id").GetInt64();
                bestFans = fans;
            }
        }

        if (deezerId is null)
        {
            return ([], 0);
        }

        var top = await GetAsync(http, $"artist/{deezerId}/top?limit={TopTracks}", ct);
        if (top is null || !top.Value.TryGetProperty("data", out var tracks))
        {
            return ([], bestFans);
        }

        return (tracks.EnumerateArray()
            .Select(t => (Title: TitleCore(t.GetProperty("title").GetString() ?? ""), Rank: t.TryGetProperty("rank", out var r) ? r.GetInt32() : 0))
            .Where(t => t.Title.Length > 0 && t.Rank > 0)
            .ToList(), bestFans);
    }

    private async Task<JsonElement?> GetAsync(HttpClient http, string path, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 6; attempt++)
        {
            await WaitTurnAsync(ct);
            try
            {
                using var response = await http.GetAsync(path, ct);
                if ((int)response.StatusCode >= 500)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
                    continue;
                }

                var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement.Clone();
                // Deezer reports throttling as a 200 with {"error": {"code": 4}}.
                if (body.TryGetProperty("error", out var error))
                {
                    if (error.TryGetProperty("code", out var code) && code.GetInt32() == 4)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), ct);
                        continue;
                    }

                    return null;
                }

                return body;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
            }
        }

        return null;
    }

    private async Task WaitTurnAsync(CancellationToken ct)
    {
        await _pace.WaitAsync(ct);
        try
        {
            var wait = _nextRequest - DateTime.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, ct);
            }

            _nextRequest = DateTime.UtcNow + RequestSpacing;
        }
        finally
        {
            _pace.Release();
        }
    }

    // The artist's recording for each Deezer title; where several recordings
    // share the title, the one ListenBrainz users listened to most.
    private static async Task<List<(long RecordingId, int Rank)>> MatchAsync(
        NpgsqlConnection connection, long artistId, List<(string Title, int Rank)> tracks, CancellationToken ct)
    {
        var byTitle = tracks.GroupBy(t => t.Title).ToDictionary(g => g.Key, g => g.Max(t => t.Rank));
        await using var command = new NpgsqlCommand("""
            SELECT DISTINCT ON (t) r.id, t
            FROM catalog.recording r
            CROSS JOIN LATERAL (SELECT lower(translate(r.name, '’‘‐‑–—', '''''----')) AS t) k
            LEFT JOIN catalog.recording_popularity p ON p.recording_id = r.id
            WHERE r.artist_id = @artist AND t = ANY(@titles)
            ORDER BY t, p.listeners DESC NULLS LAST, r.id
            """, connection);
        command.Parameters.AddWithValue("artist", artistId);
        command.Parameters.Add(new NpgsqlParameter("titles", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = byTitle.Keys.ToArray() });

        var matches = new List<(long, int)>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            matches.Add((reader.GetInt64(0), byTitle[reader.GetString(1)]));
        }

        return matches;
    }

    private static async Task StoreAsync(
        NpgsqlConnection connection, List<(long RecordingId, int Rank, long Fans)> results, int progress, CancellationToken ct)
    {
        await using var transaction = await connection.BeginTransactionAsync(ct);
        var best = results.GroupBy(r => r.RecordingId)
            .Select(g => (Id: g.Key, Rank: g.Max(r => r.Rank), Fans: g.Max(r => r.Fans))).ToList();
        if (best.Count > 0)
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO catalog.recording_deezer (recording_id, deezer_rank, artist_fans)
                SELECT * FROM unnest(@ids, @ranks, @fans)
                ON CONFLICT (recording_id) DO UPDATE
                    SET deezer_rank = excluded.deezer_rank, artist_fans = excluded.artist_fans
                """, connection, transaction);
            insert.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = best.Select(b => b.Id).ToArray() });
            insert.Parameters.Add(new NpgsqlParameter("ranks", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = best.Select(b => b.Rank).ToArray() });
            insert.Parameters.Add(new NpgsqlParameter("fans", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = best.Select(b => b.Fans).ToArray() });
            await insert.ExecuteNonQueryAsync(ct);
        }

        await using var mark = new NpgsqlCommand("""
            INSERT INTO catalog.popularity_progress (entity, last_id, updated_at) VALUES (@key, @done, now())
            ON CONFLICT (entity) DO UPDATE SET last_id = excluded.last_id, updated_at = now()
            """, connection, transaction);
        mark.Parameters.AddWithValue("key", ProgressKey);
        mark.Parameters.AddWithValue("done", (long)progress);
        await mark.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static async Task<List<(long Id, string Name)>> ArtistsAsync(NpgsqlConnection connection, int count, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT a.id, a.name FROM catalog.artist_popularity p JOIN catalog.artist a ON a.id = p.artist_id
            ORDER BY p.listeners DESC, a.id LIMIT @count
            """, connection);
        command.Parameters.AddWithValue("count", count);
        var artists = new List<(long, string)>(count);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            artists.Add((reader.GetInt64(0), reader.GetString(1)));
        }

        return artists;
    }

    private static async Task<int> ProgressAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT last_id FROM catalog.popularity_progress WHERE entity = @key", connection);
        command.Parameters.AddWithValue("key", ProgressKey);
        return await command.ExecuteScalarAsync(ct) is long done ? (int)done : 0;
    }

    private static string Normalize(string name) => Punctuation().Replace(name.ToLowerInvariant(), "").Trim();

    // Deezer titles carry versions the catalog does not: "Song (Remastered
    // 2011)", "Song - Live". Match on the part before them, lower-cased.
    public static string TitleCore(string title)
    {
        var core = Parenthetical().Replace(title, "");
        var dash = core.IndexOf(" - ", StringComparison.Ordinal);
        if (dash > 0)
        {
            core = core[..dash];
        }

        // MusicBrainz uses typographic apostrophes and hyphens ("Anti‐Hero");
        // Deezer mostly does not. Fold both sides to ASCII.
        foreach (var typographic in "‐‑–—")
        {
            core = core.Replace(typographic, '-');
        }

        return core.Replace('’', '\'').Replace('‘', '\'').Trim().ToLowerInvariant();
    }

    [GeneratedRegex(@"\s*[\(\[][^\)\]]*[\)\]]")]
    private static partial Regex Parenthetical();

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex Punctuation();
}
