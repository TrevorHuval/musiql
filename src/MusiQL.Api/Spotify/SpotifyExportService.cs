using Microsoft.EntityFrameworkCore;
using MusiQL.Api.Contracts;
using MusiQL.Api.Query;
using MusiQL.Core.Mql.Execution;
using MusiQL.Data.App;

namespace MusiQL.Api.Spotify;

public sealed class SpotifyExportService(
    QueryService query,
    TrackMatcher matcher,
    ISpotifyClientFactory clientFactory,
    AppDbContext db)
{
    private static readonly TimeSpan NegativeMatchTtl = TimeSpan.FromDays(7);

    public async Task<ExportResponse> ExportAsync(Playlist playlist, bool rematch, CancellationToken ct)
    {
        var compilation = query.Compile(playlist.MqlText, playlist.OwnerId);
        if (!compilation.Success)
        {
            throw new ExportNotSupportedException("This playlist's query is not valid.");
        }

        var compiled = compilation.Query!;
        if (compiled.Entity != "tracks")
        {
            throw new ExportNotSupportedException(
                "Only track playlists can be exported to Spotify. Switch the source to tracks first.");
        }

        var result = await query.ExecuteAsync(compiled, ct);
        var rows = ExtractRecordings(result);

        var session = await clientFactory.ForUserAsync(playlist.OwnerId, ct);

        var uris = new List<string>(rows.Count);
        var unmatched = new List<UnmatchedTrackDto>();
        foreach (var row in rows)
        {
            var match = await ResolveMatchAsync(row.Recording, session.Client, rematch, ct);
            if (match.Matched)
            {
                uris.Add(match.SpotifyUri!);
            }
            else
            {
                unmatched.Add(new UnmatchedTrackDto(
                    row.Recording.Title, row.Recording.Artist, row.Year, match.Confidence));
            }
        }

        await db.SaveChangesAsync(ct);

        var target = await UpsertPlaylistAsync(playlist, session, ct);
        await session.Client.ReplacePlaylistItemsAsync(target.Ref.Id, uris, ct);

        target.Link.TrackCount = uris.Count;
        target.Link.LastExportedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new ExportResponse(
            playlist.Name,
            target.Ref.Id,
            target.Ref.Url,
            uris.Count,
            uris.Count,
            rows.Count,
            unmatched,
            target.Link.LastExportedAt);
    }

    private async Task<(SpotifyPlaylistRef Ref, SpotifyPlaylistLink Link)> UpsertPlaylistAsync(
        Playlist playlist, SpotifyUserSession session, CancellationToken ct)
    {
        var description = Description(playlist);
        var link = await db.SpotifyPlaylistLinks.FirstOrDefaultAsync(l => l.PlaylistId == playlist.Id, ct);

        if (link is not null)
        {
            var existing = await session.Client.GetPlaylistAsync(link.SpotifyPlaylistId, ct);
            if (existing is not null)
            {
                await session.Client.UpdatePlaylistDetailsAsync(existing.Id, playlist.Name, description, ct);
                return (existing with { Name = playlist.Name }, link);
            }
        }

        var created = await session.Client.CreatePlaylistAsync(
            session.SpotifyUserId, playlist.Name, description, ct);

        if (link is null)
        {
            link = new SpotifyPlaylistLink { PlaylistId = playlist.Id, UserId = playlist.OwnerId };
            db.SpotifyPlaylistLinks.Add(link);
        }

        link.SpotifyPlaylistId = created.Id;
        return (created, link);
    }

    private async Task<TrackMatchResult> ResolveMatchAsync(
        RecordingRef recording, ISpotifySearch search, bool rematch, CancellationToken ct)
    {
        if (!rematch)
        {
            var cached = await db.TrackMatches.FindAsync([recording.Mbid], ct);
            if (cached is not null && (cached.SpotifyTrackId is not null || Fresh(cached)))
            {
                return new TrackMatchResult(
                    cached.SpotifyTrackId, cached.SpotifyUri, cached.Confidence, Method(cached.Method), cached.Isrc);
            }
        }

        var match = await matcher.MatchAsync(recording, search, ct);
        await UpsertMatchAsync(recording.Mbid, match, ct);
        return match;
    }

    private async Task UpsertMatchAsync(Guid mbid, TrackMatchResult match, CancellationToken ct)
    {
        var existing = await db.TrackMatches.FindAsync([mbid], ct);
        if (existing is null)
        {
            existing = new TrackMatch { RecordingMbid = mbid };
            db.TrackMatches.Add(existing);
        }

        existing.SpotifyTrackId = match.SpotifyTrackId;
        existing.SpotifyUri = match.SpotifyUri;
        existing.Confidence = match.Confidence;
        existing.Method = match.Method.ToString().ToLowerInvariant();
        existing.Isrc = match.Isrc;
        existing.MatchedAt = DateTime.UtcNow;
    }

    private static bool Fresh(TrackMatch match) => DateTime.UtcNow - match.MatchedAt < NegativeMatchTtl;

    private static MatchMethod Method(string value) =>
        Enum.TryParse<MatchMethod>(value, ignoreCase: true, out var method) ? method : MatchMethod.None;

    private static string Description(Playlist playlist) =>
        Truncate(string.IsNullOrWhiteSpace(playlist.Description)
            ? "A live playlist generated by MusiQL."
            : playlist.Description.Trim());

    private static string Truncate(string value) => value.Length <= 300 ? value : value[..300];

    private static IReadOnlyList<(RecordingRef Recording, int? Year)> ExtractRecordings(QueryResult result)
    {
        var index = new Dictionary<string, int>();
        for (var i = 0; i < result.Columns.Count; i++)
        {
            index[result.Columns[i].Name] = i;
        }

        var rows = new List<(RecordingRef, int?)>(result.Rows.Count);
        foreach (var row in result.Rows)
        {
            var mbid = (Guid)row[index["id"]]!;
            var title = row[index["title"]] as string ?? "";
            var artist = row[index["artist"]] as string ?? "";
            var length = AsInt(row[index["length_ms"]]);
            var year = AsInt(row[index["year"]]);
            rows.Add((new RecordingRef(mbid, title, artist, length), year));
        }

        return rows;
    }

    private static int? AsInt(object? value) => value switch
    {
        null => null,
        int i => i,
        short s => s,
        long l => (int)l,
        _ => Convert.ToInt32(value)
    };
}
