using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MusiQL.Api.Auth;
using MusiQL.Api.Contracts;
using MusiQL.Api.Errors;
using MusiQL.Api.Query;
using MusiQL.Api.Spotify;
using MusiQL.Data.App;

namespace MusiQL.Api.Endpoints;

public static class PlaylistEndpoints
{
    public static RouteGroupBuilder MapPlaylistEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", List);
        group.MapPost("/", Create).RequireRateLimiting(RateLimits.Write);
        group.MapGet("/{id:guid}", Get);
        group.MapPut("/{id:guid}", Update).RequireRateLimiting(RateLimits.Write);
        group.MapDelete("/{id:guid}", Delete).RequireRateLimiting(RateLimits.Write);
        group.MapGet("/{id:guid}/tracks", Tracks).RequireRateLimiting(RateLimits.Query);
        group.MapGet("/{id:guid}/export/m3u", ExportM3u).RequireRateLimiting(RateLimits.Query);
        group.MapPost("/{id:guid}/export/spotify", ExportToSpotify).RequireRateLimiting(RateLimits.Query);
        group.MapGet("/{id:guid}/spotify", SpotifyLink);
        group.MapPut("/{id:guid}/spotify/keep-live", SetKeepLive).RequireRateLimiting(RateLimits.Write);
        return group;
    }

    private static async Task<IResult> List(ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var userId = principal.UserId()!.Value;
        var playlists = await db.Playlists
            .Where(p => p.OwnerId == userId)
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => ToResponse(p))
            .ToListAsync(ct);

        return Results.Ok(playlists);
    }

    private static async Task<IResult> Create(
        PlaylistRequest request, ClaimsPrincipal principal, AppDbContext db, QueryService query,
        IOptions<LimitsOptions> limits, CancellationToken ct)
    {
        var userId = principal.UserId()!.Value;
        if (Validate(request) is { } problem)
        {
            return problem;
        }

        var compilation = query.Compile(request.Mql, userId);
        if (!compilation.Success)
        {
            return ApiProblems.MqlValidation(compilation.Errors);
        }

        var max = limits.Value.MaxPlaylistsPerUser;
        if (max > 0 && await db.Playlists.CountAsync(p => p.OwnerId == userId, ct) >= max)
        {
            return ApiProblems.QuotaExceeded($"You can keep up to {max} playlists. Delete one to make room.");
        }

        var now = DateTime.UtcNow;
        var playlist = new Playlist
        {
            Id = Guid.CreateVersion7(),
            OwnerId = userId,
            Name = request.Name.Trim(),
            Description = request.Description,
            MqlText = request.Mql,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Playlists.Add(playlist);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/playlists/{playlist.Id}", ToResponse(playlist));
    }

    private static async Task<IResult> Get(
        Guid id, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var playlist = await Owned(db, principal, id, ct);
        return playlist is null ? ApiProblems.NotFound("Playlist") : Results.Ok(ToResponse(playlist));
    }

    private static async Task<IResult> Update(
        Guid id, PlaylistRequest request, ClaimsPrincipal principal, AppDbContext db, QueryService query,
        CancellationToken ct)
    {
        var playlist = await Owned(db, principal, id, ct);
        if (playlist is null)
        {
            return ApiProblems.NotFound("Playlist");
        }

        if (Validate(request) is { } problem)
        {
            return problem;
        }

        var compilation = query.Compile(request.Mql, playlist.OwnerId);
        if (!compilation.Success)
        {
            return ApiProblems.MqlValidation(compilation.Errors);
        }

        playlist.Name = request.Name.Trim();
        playlist.Description = request.Description;
        playlist.MqlText = request.Mql;
        playlist.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(ToResponse(playlist));
    }

    private static async Task<IResult> Delete(
        Guid id, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var playlist = await Owned(db, principal, id, ct);
        if (playlist is null)
        {
            return ApiProblems.NotFound("Playlist");
        }

        db.Playlists.Remove(playlist);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Tracks(
        Guid id, int? page, int? pageSize, ClaimsPrincipal principal, AppDbContext db, QueryService query,
        PlaylistSnapshotService snapshots, CancellationToken ct)
    {
        var playlist = await Owned(db, principal, id, ct);
        if (playlist is null)
        {
            return ApiProblems.NotFound("Playlist");
        }

        var compilation = query.Compile(playlist.MqlText, playlist.OwnerId);
        if (!compilation.Success)
        {
            return ApiProblems.MqlValidation(compilation.Errors);
        }

        var result = await snapshots.GetPageAsync(
            playlist, compilation.Query!, page ?? 1, pageSize ?? QueryService.DefaultPageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ExportM3u(
        Guid id, ClaimsPrincipal principal, AppDbContext db, QueryService query, CancellationToken ct)
    {
        var playlist = await Owned(db, principal, id, ct);
        if (playlist is null)
        {
            return ApiProblems.NotFound("Playlist");
        }

        var compilation = query.Compile(playlist.MqlText, playlist.OwnerId);
        if (!compilation.Success)
        {
            return ApiProblems.MqlValidation(compilation.Errors);
        }

        if (compilation.Query!.Entity != "tracks")
        {
            return ApiProblems.ExportUnsupported("An M3U file can only be built from a track playlist.");
        }

        var result = await query.ExecuteAsync(compilation.Query, ct);
        var m3u = Encoding.UTF8.GetBytes(M3uExport.Build(result));
        return Results.File(m3u, "audio/x-mpegurl", $"{FileName(playlist.Name)}.m3u8");
    }

    private static string FileName(string name)
    {
        var slug = new string(name.Trim().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray())
            .Trim('-');
        return slug.Length == 0 ? "playlist" : slug;
    }

    private static async Task<IResult> ExportToSpotify(
        Guid id, bool? rematch, ClaimsPrincipal principal, AppDbContext db, SpotifyExportService export,
        OperationLocks locks, CancellationToken ct)
    {
        var playlist = await Owned(db, principal, id, ct);
        if (playlist is null)
        {
            return ApiProblems.NotFound("Playlist");
        }

        try
        {
            using var _ = locks.Acquire($"export:{playlist.Id}", "An export of this playlist");
            var result = await export.ExportAsync(playlist, rematch ?? false, ct);
            return Results.Ok(result);
        }
        catch (Exception ex) when (ex is SpotifyNotConfiguredException or SpotifyNotConnectedException
            or ExportNotSupportedException or SpotifyApiException)
        {
            return ApiProblems.Spotify(ex);
        }
    }

    private static async Task<IResult> SpotifyLink(
        Guid id, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var playlist = await Owned(db, principal, id, ct);
        if (playlist is null)
        {
            return ApiProblems.NotFound("Playlist");
        }

        var link = await db.SpotifyPlaylistLinks.AsNoTracking().FirstOrDefaultAsync(l => l.PlaylistId == id, ct);
        return Results.Ok(ToLinkResponse(link));
    }

    private static async Task<IResult> SetKeepLive(
        Guid id, KeepLiveRequest request, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var playlist = await Owned(db, principal, id, ct);
        if (playlist is null)
        {
            return ApiProblems.NotFound("Playlist");
        }

        var link = await db.SpotifyPlaylistLinks.FirstOrDefaultAsync(l => l.PlaylistId == id, ct);
        if (link is null)
        {
            return Results.Problem(
                title: "Export first",
                detail: "Export this playlist to Spotify once before keeping it live.",
                statusCode: StatusCodes.Status409Conflict);
        }

        link.KeepLive = request.Enabled;
        if (request.Enabled)
        {
            link.LastRefreshError = null;
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToLinkResponse(link));
    }

    private static PlaylistSpotifyLinkResponse ToLinkResponse(SpotifyPlaylistLink? link) => link is null
        ? new PlaylistSpotifyLinkResponse(false, null, 0, null, false, null, null)
        : new PlaylistSpotifyLinkResponse(
            true,
            $"https://open.spotify.com/playlist/{link.SpotifyPlaylistId}",
            link.TrackCount,
            link.LastExportedAt,
            link.KeepLive,
            link.KeepLive ? link.LastExportedAt + LivePlaylistRefresher.RefreshEvery : null,
            link.LastRefreshError);

    // Column limits from AppDbContext, checked here so an oversized save is a
    // 400 with a reason instead of a database error.
    private static IResult? Validate(PlaylistRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiProblems.Invalid("Name is required");
        }

        if (request.Name.Trim().Length > Playlist.MaxNameLength)
        {
            return ApiProblems.Invalid("Name is too long", $"Names are limited to {Playlist.MaxNameLength} characters.");
        }

        if (request.Description?.Length > Playlist.MaxDescriptionLength)
        {
            return ApiProblems.Invalid(
                "Description is too long", $"Descriptions are limited to {Playlist.MaxDescriptionLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(request.Mql))
        {
            return ApiProblems.Invalid("A query is required");
        }

        if (request.Mql.Length > Playlist.MaxMqlLength)
        {
            return ApiProblems.Invalid("Query is too long", $"Queries are limited to {Playlist.MaxMqlLength:N0} characters.");
        }

        return null;
    }

    private static Task<Playlist?> Owned(AppDbContext db, ClaimsPrincipal principal, Guid id, CancellationToken ct)
    {
        var userId = principal.UserId()!.Value;
        return db.Playlists.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == userId, ct);
    }

    private static PlaylistResponse ToResponse(Playlist p) =>
        new(p.Id, p.Name, p.Description, p.MqlText, p.CreatedAt, p.UpdatedAt);
}
