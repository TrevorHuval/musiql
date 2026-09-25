using Microsoft.EntityFrameworkCore;
using MusiQL.Api.Spotify;
using MusiQL.Data;
using MusiQL.Data.App;
using MusiQL.Tests.Api;
using Xunit;

namespace MusiQL.Tests.Spotify;

[Collection("api")]
public class SpotifyLibraryServiceTests(ApiFixture fixture)
{
    [Fact]
    public async Task Sync_matches_saved_tracks_into_library()
    {
        if (!fixture.Available)
        {
            return;
        }

        var ownerId = await ConnectedOwnerAsync();
        var client = new FakeSpotifyClient
        {
            Saved =
            [
                Saved("sp1", "Smells Like Teen Spirit", "Nirvana", 301000),
                Saved("sp2", "Come as You Are", "Nirvana", 219000),
                Saved("sp3", "Totally Unknown Track", "Nobody At All", 123000)
            ]
        };

        var result = await BuildLibrary(client).SyncAsync(ownerId, default);

        Assert.Equal(3, result.SavedCount);
        Assert.Equal(2, result.MatchedCount);
        Assert.Equal(1, result.UnmatchedCount);
        Assert.Equal(2, result.LibraryCount);
        Assert.Equal(2, await LibraryCountAsync(ownerId));
    }

    [Fact]
    public async Task Re_sync_removes_unsaved_tracks_from_library()
    {
        if (!fixture.Available)
        {
            return;
        }

        var ownerId = await ConnectedOwnerAsync();
        var client = new FakeSpotifyClient
        {
            Saved =
            [
                Saved("sp1", "Smells Like Teen Spirit", "Nirvana", 301000),
                Saved("sp2", "Come as You Are", "Nirvana", 219000)
            ]
        };

        await BuildLibrary(client).SyncAsync(ownerId, default);
        Assert.Equal(2, await LibraryCountAsync(ownerId));

        client.Saved = [Saved("sp1", "Smells Like Teen Spirit", "Nirvana", 301000)];
        var result = await BuildLibrary(client).SyncAsync(ownerId, default);

        Assert.Equal(1, result.LibraryCount);
        Assert.Equal(1, await LibraryCountAsync(ownerId));

        await using var db = AppDbContextFactory.Create(fixture.ConnectionString);
        Assert.False(await db.SpotifySavedTracks.AnyAsync(t => t.UserId == ownerId && t.SpotifyTrackId == "sp2"));
    }

    [Fact]
    public async Task Exact_and_fuzzy_matches_resolve_and_misses_are_remembered()
    {
        if (!fixture.Available)
        {
            return;
        }

        var ownerId = await ConnectedOwnerAsync();
        var client = new FakeSpotifyClient
        {
            Saved =
            [
                Saved("ex1", "ALIVE", "Pearl Jam", 341000),
                Saved("fz1", "Smells Like Teen Spirit - Remastered", "Nirvana", 301000),
                Saved("no1", "Nothing Like This", "Nobody At All", 100000)
            ]
        };

        var result = await BuildLibrary(client).SyncAsync(ownerId, default);
        Assert.Equal(2, result.MatchedCount);

        await using var db = AppDbContextFactory.Create(fixture.ConnectionString);
        var rows = await db.SpotifySavedTracks.Where(t => t.UserId == ownerId).ToDictionaryAsync(t => t.SpotifyTrackId);
        Assert.Equal(1.0, rows["ex1"].Confidence);
        Assert.NotNull(rows["fz1"].RecordingId);
        Assert.Null(rows["ex1"].MatchAttemptedAt);
        Assert.Null(rows["no1"].RecordingId);
        Assert.NotNull(rows["no1"].MatchAttemptedAt);
    }

    [Fact]
    public async Task A_match_whose_id_no_longer_carries_its_mbid_is_matched_again()
    {
        if (!fixture.Available)
        {
            return;
        }

        var ownerId = await ConnectedOwnerAsync();
        var client = new FakeSpotifyClient { Saved = [Saved("st1", "Alive", "Pearl Jam", 341000)] };
        await BuildLibrary(client).SyncAsync(ownerId, default);

        long rightId;
        await using (var db = AppDbContextFactory.Create(fixture.ConnectionString))
        {
            var row = await db.SpotifySavedTracks.SingleAsync(t => t.UserId == ownerId);
            rightId = row.RecordingId!.Value;
            row.RecordingId = rightId + 1;
            await db.SaveChangesAsync();
        }

        await BuildLibrary(client).SyncAsync(ownerId, default);

        await using var check = AppDbContextFactory.Create(fixture.ConnectionString);
        Assert.Equal(rightId, (await check.SpotifySavedTracks.SingleAsync(t => t.UserId == ownerId)).RecordingId);
    }

    [Fact]
    public async Task A_near_title_needs_an_agreeing_duration()
    {
        if (!fixture.Available)
        {
            return;
        }

        var ownerId = await ConnectedOwnerAsync();
        var client = new FakeSpotifyClient
        {
            Saved =
            [
                Saved("nd1", "Smells Like Teen Spirt", "Nirvana", 301000),
                Saved("nd2", "Smells Like Teen Spirt", "Nirvana", 120000)
            ]
        };

        await BuildLibrary(client).SyncAsync(ownerId, default);

        await using var db = AppDbContextFactory.Create(fixture.ConnectionString);
        var rows = await db.SpotifySavedTracks.Where(t => t.UserId == ownerId).ToDictionaryAsync(t => t.SpotifyTrackId);
        Assert.NotNull(rows["nd1"].RecordingId);
        Assert.Null(rows["nd2"].RecordingId);
    }

    private static SpotifySavedItem Saved(string id, string title, string artist, int durationMs) =>
        new(id, title, artist, durationMs, null, DateTime.UtcNow);

    private SpotifyLibraryService BuildLibrary(FakeSpotifyClient client) => new(
        new FakeSpotifyClientFactory(client),
        new CatalogMatcher(fixture.CreateCatalogContext()),
        AppDbContextFactory.Create(fixture.ConnectionString),
        fixture.CreateCatalogContext());

    private async Task<int> LibraryCountAsync(Guid userId)
    {
        await using var catalog = MusiQLDbContextFactory.Create(fixture.ConnectionString);
        return await catalog.UserLibrary.CountAsync(l => l.UserId == userId);
    }

    private async Task<Guid> ConnectedOwnerAsync()
    {
        var auth = await fixture.RegisterAsync(fixture.CreateClient(), ApiFixture.UniqueEmail());
        await using var db = AppDbContextFactory.Create(fixture.ConnectionString);
        db.SpotifyAccounts.Add(new SpotifyAccount
        {
            UserId = auth.UserId,
            SpotifyUserId = "spotify-user",
            DisplayName = "Fake User",
            AccessTokenCipher = "x",
            RefreshTokenCipher = "x",
            Scopes = "",
            AccessExpiresAt = DateTime.UtcNow.AddHours(1),
            ConnectedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return auth.UserId;
    }
}
