using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MusiQL.Data.App;

namespace MusiQL.Api.Spotify;

public sealed class MusicBrainzIsrcLookup(
    IHttpClientFactory httpFactory,
    AppDbContext db,
    RequestThrottle throttle,
    ILogger<MusicBrainzIsrcLookup> logger) : IMusicBrainzIsrcLookup
{
    public const string HttpClientName = "musicbrainz";

    public async Task<IReadOnlyList<string>> GetIsrcsAsync(Guid recordingMbid, CancellationToken ct)
    {
        var cached = await db.RecordingIsrcs.FindAsync([recordingMbid], ct);
        if (cached is not null)
        {
            return Split(cached.Isrc);
        }

        var fetched = await FetchAsync(recordingMbid, ct);
        if (fetched is null)
        {
            return [];
        }

        db.RecordingIsrcs.Add(new RecordingIsrc
        {
            RecordingMbid = recordingMbid,
            Isrc = fetched.Count == 0 ? null : string.Join(' ', fetched),
            FetchedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
        return fetched;
    }

    private async Task<IReadOnlyList<string>?> FetchAsync(Guid recordingMbid, CancellationToken ct)
    {
        await throttle.WaitAsync(ct);
        try
        {
            var http = httpFactory.CreateClient(HttpClientName);
            using var response = await http.GetAsync(
                $"ws/2/recording/{recordingMbid}?inc=isrcs&fmt=json", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return [];
            }

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!document.RootElement.TryGetProperty("isrcs", out var isrcs) ||
                isrcs.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return isrcs.EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "MusicBrainz ISRC lookup failed for {Mbid}", recordingMbid);
            return null;
        }
    }

    private static IReadOnlyList<string> Split(string? value) =>
        string.IsNullOrWhiteSpace(value) ? [] : value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
