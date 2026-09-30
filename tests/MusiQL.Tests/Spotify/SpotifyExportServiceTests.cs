using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MusiQL.Api.Query;
using MusiQL.Api.Spotify;
using MusiQL.Core.Mql;
using MusiQL.Data.App;
using MusiQL.Tests.Api;
using Xunit;

namespace MusiQL.Tests.Spotify;

[Collection("api")]
public class SpotifyExportServiceTests(ApiFixture fixture)
{
    private const string GrungeMql =
        "tracks where genre = \"grunge\" and year between 1990 and 2004 and artist != \"Nirvana\" order by title";

    [Fact]
    public async Task Export_creates_playlist_with_matched_tracks_in_order()
    {
        if (!fixture.Available)
        {
            return;
        }

        var ownerId = await RegisterOwnerAsync();
        var playlist = await SeedPlaylistAsync(ownerId, "Grunge Essentials", GrungeMql);
        var expected = await ExpectedTitlesAsync(ownerId);
        Assert.NotEmpty(expected);

        var missing = expected[0];
        var client = EchoClient(missing);
        var export = BuildExport(client);

        var result = await export.ExportAsync(playlist, rematch: false, default);

        Assert.Single(client.Created);
        Assert.Equal("Grunge Essentials", client.Created[0].Name);
        Assert.Equal(expected.Count, result.TotalCount);
        Assert.Equal(expected.Count - expected.Count(t => t == missing), result.MatchedCount);
        Assert.Contains(result.Unmatched, u => u.Title == missing);

        var pushed = client.Playlists[client.Created[0].Id];
        var expectedUris = expected.Where(t => t != missing).Select(Uri).ToList();
        Assert.Equal(expectedUris, pushed);
    }

    [Fact]
    public async Task Re_export_reuses_playlist_and_cached_matches()
    {
        if (!fixture.Available)
        {
            return;
        }

        var ownerId = await RegisterOwnerAsync();
        var playlist = await SeedPlaylistAsync(ownerId, "Grunge Rerun", GrungeMql);
        var expected = await ExpectedTitlesAsync(ownerId);

        var client = EchoClient(expected[0]);
        var export = BuildExport(client);

        await export.ExportAsync(playlist, rematch: false, default);
        var callsAfterFirst = client.TextSearchCalls;
        var createdAfterFirst = client.Created.Count;

        var second = await export.ExportAsync(playlist, rematch: false, default);

        Assert.Equal(createdAfterFirst, client.Created.Count);
        Assert.Equal(callsAfterFirst, client.TextSearchCalls);
        Assert.Equal(expected.Count(t => t != expected[0]), second.MatchedCount);

        await using var db = AppDbContextFactory.Create(fixture.ConnectionString);
        var persisted = await db.TrackMatches.CountAsync();
        Assert.True(persisted >= expected.Count);
    }

    [Fact]
    public async Task Re_export_keeps_tracks_the_user_added_and_appends_new_ones()
    {
        if (!fixture.Available)
        {
            return;
        }

        var ownerId = await RegisterOwnerAsync();
        var playlist = await SeedPlaylistAsync(ownerId, "Grunge Custom", GrungeMql);
        var expected = await ExpectedTitlesAsync(ownerId);
        Assert.True(expected.Count >= 3);

        var client = EchoClient(null);
        var export = BuildExport(client);
        await export.ExportAsync(playlist, rematch: false, default);
        var id = client.Created[0].Id;

        const string local = "spotify:local:Me:Demos:Garage+Take:180";
        const string manual = "spotify:track:handpicked";
        client.Playlists[id].Insert(1, local);
        client.Playlists[id].Add(manual);

        // The query now drops one track, and the user deleted another from Spotify.
        var dropped = Uri(expected[0]);
        var deleted = Uri(expected[1]);
        await UpdateMqlAsync(playlist.Id, GrungeMql.Replace("order by title", "and title != \"" + expected[0] + "\" order by title"));
        client.Playlists[id].Remove(deleted);

        await BuildExport(client).ExportAsync(await LoadPlaylistAsync(playlist.Id), rematch: false, default);

        var items = client.Playlists[id];
        Assert.Contains(local, items);
        Assert.Contains(manual, items);
        Assert.DoesNotContain(dropped, items);
        Assert.Contains(deleted, items);
        Assert.Equal(deleted, items[^1]);
    }

    [Fact]
    public async Task First_re_export_of_a_ledgerless_link_claims_spotify_tracks_but_not_local_files()
    {
        if (!fixture.Available)
        {
            return;
        }

        var ownerId = await RegisterOwnerAsync();
        var playlist = await SeedPlaylistAsync(ownerId, "Grunge Legacy", GrungeMql);
        var expected = await ExpectedTitlesAsync(ownerId);

        var client = EchoClient(null);
        await BuildExport(client).ExportAsync(playlist, rematch: true, default);
        var id = client.Created[0].Id;

        // Simulate a link exported before the ledger existed.
        await using (var db = AppDbContextFactory.Create(fixture.ConnectionString))
        {
            db.SpotifyPlaylistLinkTracks.RemoveRange(db.SpotifyPlaylistLinkTracks.Where(t => t.PlaylistId == playlist.Id));
            await db.SaveChangesAsync();
        }

        const string local = "spotify:local:Me:Demos:Garage+Take:180";
        client.Playlists[id].Add(local);
        client.Playlists[id].Add("spotify:track:stale");

        await BuildExport(client).ExportAsync(playlist, rematch: true, default);

        var items = client.Playlists[id];
        Assert.Contains(local, items);
        Assert.DoesNotContain("spotify:track:stale", items);
        Assert.Equal(expected.Distinct().Count(), items.Count(u => u != local));
    }

    [Fact]
    public async Task Export_rejects_non_track_playlists()
    {
        if (!fixture.Available)
        {
            return;
        }

        var ownerId = await RegisterOwnerAsync();
        var playlist = await SeedPlaylistAsync(ownerId, "Albums", "albums where genre = \"grunge\"");
        var export = BuildExport(EchoClient(null));

        await Assert.ThrowsAsync<ExportNotSupportedException>(
            () => export.ExportAsync(playlist, rematch: false, default));
    }

    private static FakeSpotifyClient EchoClient(string? unmatchedTitle) => new()
    {
        TextSearch = (title, artist) => title == unmatchedTitle
            ? []
            : [new SpotifyTrack($"id-{title}", Uri(title), title, [artist], null, null)]
    };

    private SpotifyExportService BuildExport(FakeSpotifyClient client)
    {
        var options = Options.Create(new QueryOptions { ConnectionString = fixture.ConnectionString });
        var query = new QueryService(
            MqlEngine.CreateDefault(), fixture.CreateCatalogContext(), new QueryGate(options), options);
        return new SpotifyExportService(
            query,
            new TrackMatcher(new FakeIsrcLookup()),
            new FakeSpotifyClientFactory(client),
            AppDbContextFactory.Create(fixture.ConnectionString));
    }

    private async Task<IReadOnlyList<string>> ExpectedTitlesAsync(Guid ownerId)
    {
        var options = Options.Create(new QueryOptions { ConnectionString = fixture.ConnectionString });
        var query = new QueryService(
            MqlEngine.CreateDefault(), fixture.CreateCatalogContext(), new QueryGate(options), options);
        var compiled = query.Compile(GrungeMql, ownerId);
        var result = await query.ExecuteAsync(compiled.Query!, default);
        var titleIndex = result.Columns.Select((c, i) => (c.Name, i)).First(x => x.Name == "title").i;
        return result.Rows.Select(r => (string)r[titleIndex]!).ToList();
    }

    private async Task<Guid> RegisterOwnerAsync()
    {
        var auth = await fixture.RegisterAsync(fixture.CreateClient(), ApiFixture.UniqueEmail());
        return auth.UserId;
    }

    private async Task<Playlist> SeedPlaylistAsync(Guid ownerId, string name, string mql)
    {
        await using var db = AppDbContextFactory.Create(fixture.ConnectionString);
        var now = DateTime.UtcNow;
        var playlist = new Playlist
        {
            Id = Guid.CreateVersion7(),
            OwnerId = ownerId,
            Name = name,
            MqlText = mql,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync();
        return playlist;
    }

    private async Task UpdateMqlAsync(Guid playlistId, string mql)
    {
        await using var db = AppDbContextFactory.Create(fixture.ConnectionString);
        var stored = await db.Playlists.SingleAsync(p => p.Id == playlistId);
        stored.MqlText = mql;
        await db.SaveChangesAsync();
    }

    private async Task<Playlist> LoadPlaylistAsync(Guid playlistId)
    {
        await using var db = AppDbContextFactory.Create(fixture.ConnectionString);
        return await db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlistId);
    }

    private static string Uri(string title) => $"spotify:track:{title}";
}
