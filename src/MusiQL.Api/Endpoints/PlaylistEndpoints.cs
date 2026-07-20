using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
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
        group.MapPost("/", Create);
        group.MapGet("/{id:guid}", Get);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
        group.MapGet("/{id:guid}/tracks", Tracks).RequireRateLimiting(RateLimits.Query);
        group.MapPost("/{id:guid}/export/spotify", ExportToSpotify).RequireRateLimiting(RateLimits.Query);
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
        PlaylistRequest request, ClaimsPrincipal principal, AppDbContext db, QueryService query, CancellationToken ct)
    {
        var userId = principal.UserId()!.Value;
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.Problem(title: "Name is required", statusCode: StatusCodes.Status400BadRequest);
        }

        var compilation = query.Compile(request.Mql ?? "", userId);
        if (!compilation.Success)
        {
            return ApiProblems.MqlValidation(compilation.Errors);
        }

        var now = DateTime.UtcNow;
        var playlist = new Playlist
        {
            Id = Guid.CreateVersion7(),
            OwnerId = userId,
            Name = request.Name.Trim(),
            Description = request.Description,
            MqlText = request.Mql!,
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

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.Problem(title: "Name is required", statusCode: StatusCodes.Status400BadRequest);
        }

        var compilation = query.Compile(request.Mql ?? "", playlist.OwnerId);
        if (!compilation.Success)
        {
            return ApiProblems.MqlValidation(compilation.Errors);
        }

        playlist.Name = request.Name.Trim();
        playlist.Description = request.Description;
        playlist.MqlText = request.Mql!;
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

    private static async Task<IResult> ExportToSpotify(
        Guid id, bool? rematch, ClaimsPrincipal principal, AppDbContext db, SpotifyExportService export,
        CancellationToken ct)
    {
        var playlist = await Owned(db, principal, id, ct);
        if (playlist is null)
        {
            return ApiProblems.NotFound("Playlist");
        }

        try
        {
            var result = await export.ExportAsync(playlist, rematch ?? false, ct);
            return Results.Ok(result);
        }
        catch (Exception ex) when (ex is SpotifyNotConfiguredException or SpotifyNotConnectedException
            or ExportNotSupportedException or SpotifyApiException)
        {
            return ApiProblems.Spotify(ex);
        }
    }

    private static Task<Playlist?> Owned(AppDbContext db, ClaimsPrincipal principal, Guid id, CancellationToken ct)
    {
        var userId = principal.UserId()!.Value;
        return db.Playlists.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == userId, ct);
    }

    private static PlaylistResponse ToResponse(Playlist p) =>
        new(p.Id, p.Name, p.Description, p.MqlText, p.CreatedAt, p.UpdatedAt);
}
