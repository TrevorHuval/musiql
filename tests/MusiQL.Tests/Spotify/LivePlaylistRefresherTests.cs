using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MusiQL.Api;
using MusiQL.Api.Query;
using MusiQL.Api.Spotify;
using MusiQL.Core.Mql;
using MusiQL.Data;
using MusiQL.Data.App;
using MusiQL.Tests.Api;
using Xunit;

namespace MusiQL.Tests.Spotify;

[Collection("api")]
public class LivePlaylistRefresherTests(ApiFixture fixture)
{
    private const string Mql = "tracks where genre = \"grunge\" order by title";

    [Fact]
    public async Task Refreshes_stale_keep_live_playlists_and_skips_fresh_or_opted_out_ones()
    {
        if (!fixture.Available)
        {
            return;
        }

        var client = new FakeSpotifyClient { TextSearch = (title, artist) => [Track(title, artist)] };
        var ownerId = await OwnerAsync();
        var stale = await ExportedPlaylistAsync(ownerId, client, keepLive: true, exportedAgo: TimeSpan.FromDays(2));
        var fresh = await ExportedPlaylistAsync(ownerId, client, keepLive: true, exportedAgo: TimeSpan.FromHours(2));
        var optedOut = await ExportedPlaylistAsync(ownerId, client, keepLive: false, exportedAgo: TimeSpan.FromDays(3));

        var refreshed = await Refresher(client).RunDueAsync(default);

        Assert.True(refreshed >= 1);
        await using var db = AppDbContextFactory.Create(fixture.ConnectionString);
        var links = await db.SpotifyPlaylistLinks
            .Where(l => l.PlaylistId == stale || l.PlaylistId == fresh || l.PlaylistId == optedOut)
            .ToDictionaryAsync(l => l.PlaylistId);

        Assert.True(links[stale].LastExportedAt > DateTime.UtcNow.AddMinutes(-5));
        Assert.Null(links[stale].LastRefreshError);
        Assert.True(links[fresh].LastExportedAt < DateTime.UtcNow.AddHours(-1));
        Assert.True(links[optedOut].LastExportedAt < DateTime.UtcNow.AddDays(-2));
    }

    [Fact]
    public async Task A_failure_is_recorded_and_not_retried_until_the_back_off_passes()
    {
        if (!fixture.Available)
        {
            return;
        }

        var client = new FakeSpotifyClient { TextSearch = (title, artist) => [Track(title, artist)] };
        var ownerId = await OwnerAsync();
        var playlistId = await ExportedPlaylistAsync(ownerId, client, keepLive: true, exportedAgo: TimeSpan.FromDays(2));

        var disconnected = Refresher(client, new DisconnectedFactory());
        await disconnected.RunDueAsync(default);

        await using (var db = AppDbContextFactory.Create(fixture.ConnectionString))
        {
            var link = await db.SpotifyPlaylistLinks.SingleAsync(l => l.PlaylistId == playlistId);
            Assert.Contains("no longer connected", link.LastRefreshError);
            Assert.True(link.KeepLive);
        }

        var calls = client.Replaced.Count;
        await Refresher(client).RunDueAsync(default);
        Assert.Equal(calls, client.Replaced.Count);
    }

    [Fact]
    public async Task Syncs_the_library_of_stale_accounts_that_have_synced_before()
    {
        if (!fixture.Available)
        {
            return;
        }

        var client = new FakeSpotifyClient
        {
            Saved = [new SpotifySavedItem("live1", "Smells Like Teen Spirit", "Nirvana", 301000, null, DateTime.UtcNow)]
        };
        var stale = await ConnectedOwnerAsync(librarySyncedAgo: TimeSpan.FromDays(2));
        var fresh = await ConnectedOwnerAsync(librarySyncedAgo: TimeSpan.FromHours(2));
        var never = await ConnectedOwnerAsync(librarySyncedAgo: null);

        var synced = await Refresher(client).SyncLibrariesAsync(default);

        Assert.True(synced >= 1);
        Assert.Equal(1, await LibraryCountAsync(stale));
        Assert.Equal(0, await LibraryCountAsync(fresh));
        Assert.Equal(0, await LibraryCountAsync(never));
        await using var db = AppDbContextFactory.Create(fixture.ConnectionString);
        var account = await db.SpotifyAccounts.SingleAsync(a => a.UserId == stale);
        Assert.True(account.LibrarySyncedAt > DateTime.UtcNow.AddMinutes(-5));
        Assert.Equal(1, account.LibraryMatchedCount);
    }

    [Fact]
    public async Task A_failed_library_sync_is_not_retried_until_the_back_off_passes()
    {
        if (!fixture.Available)
        {
            return;
        }

        var ownerId = await ConnectedOwnerAsync(librarySyncedAgo: TimeSpan.FromDays(2));
        var factory = new FailingFactory();
        var refresher = Refresher(new FakeSpotifyClient(), factory);

        await refresher.SyncLibrariesAsync(default);
        await refresher.SyncLibrariesAsync(default);

        Assert.Equal(1, factory.CallsFor(ownerId));
        Assert.Equal(0, await LibraryCountAsync(ownerId));
    }

    private LivePlaylistRefresher Refresher(FakeSpotifyClient client, ISpotifyClientFactory? factory = null)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => AppDbContextFactory.Create(fixture.ConnectionString));
        services.AddScoped(_ => MusiQLDbContextFactory.Create(fixture.ConnectionString));
        services.AddSingleton(MqlEngine.CreateDefault());
        services.AddSingleton(Options.Create(new QueryOptions { ConnectionString = fixture.ConnectionString }));
        services.AddSingleton<QueryGate>();
        services.AddScoped<QueryService>();
        services.AddScoped(_ => new TrackMatcher(new FakeIsrcLookup()));
        services.AddScoped(_ => factory ?? new FakeSpotifyClientFactory(client));
        services.AddScoped<SpotifyExportService>();
        services.AddScoped(_ => new CatalogMatcher(MusiQLDbContextFactory.Create(fixture.ConnectionString)));
        services.AddScoped<SpotifyLibraryService>();
        var provider = services.BuildServiceProvider();

        return new LivePlaylistRefresher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new OperationLocks(),
            NullLogger<LivePlaylistRefresher>.Instance,
            TimeProvider.System);
    }

    private async Task<Guid> ExportedPlaylistAsync(
        Guid ownerId, FakeSpotifyClient client, bool keepLive, TimeSpan exportedAgo)
    {
        var playlist = new Playlist
        {
            Id = Guid.CreateVersion7(),
            OwnerId = ownerId,
            Name = $"Live {Guid.NewGuid():N}",
            MqlText = Mql,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await using (var db = AppDbContextFactory.Create(fixture.ConnectionString))
        {
            db.Playlists.Add(playlist);
            await db.SaveChangesAsync();
        }

        var export = new SpotifyExportService(
            new QueryService(
                MqlEngine.CreateDefault(),
                MusiQLDbContextFactory.Create(fixture.ConnectionString),
                new QueryGate(Options.Create(new QueryOptions())),
                Options.Create(new QueryOptions { ConnectionString = fixture.ConnectionString })),
            new TrackMatcher(new FakeIsrcLookup()),
            new FakeSpotifyClientFactory(client),
            AppDbContextFactory.Create(fixture.ConnectionString));
        await export.ExportAsync(playlist, rematch: false, default);

        await using var update = AppDbContextFactory.Create(fixture.ConnectionString);
        var link = await update.SpotifyPlaylistLinks.SingleAsync(l => l.PlaylistId == playlist.Id);
        link.KeepLive = keepLive;
        link.LastExportedAt = DateTime.UtcNow - exportedAgo;
        await update.SaveChangesAsync();
        return playlist.Id;
    }

    private async Task<Guid> ConnectedOwnerAsync(TimeSpan? librarySyncedAgo)
    {
        var ownerId = await OwnerAsync();
        await using var db = AppDbContextFactory.Create(fixture.ConnectionString);
        db.SpotifyAccounts.Add(new SpotifyAccount
        {
            UserId = ownerId,
            SpotifyUserId = $"spotify-{ownerId:N}",
            AccessTokenCipher = "x",
            RefreshTokenCipher = "x",
            Scopes = "",
            AccessExpiresAt = DateTime.UtcNow.AddHours(1),
            ConnectedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LibrarySyncedAt = librarySyncedAgo is { } ago ? DateTime.UtcNow - ago : null
        });
        await db.SaveChangesAsync();
        return ownerId;
    }

    private async Task<int> LibraryCountAsync(Guid userId)
    {
        await using var catalog = MusiQLDbContextFactory.Create(fixture.ConnectionString);
        return await catalog.UserLibrary.CountAsync(l => l.UserId == userId);
    }

    private async Task<Guid> OwnerAsync() =>
        (await fixture.RegisterAsync(fixture.CreateClient(), ApiFixture.UniqueEmail())).UserId;

    private static SpotifyTrack Track(string title, string artist) =>
        new($"id-{title}", $"spotify:track:id-{title}", title, [artist], null, null);

    private sealed class FailingFactory : ISpotifyClientFactory
    {
        private readonly List<Guid> _calls = [];

        public int CallsFor(Guid userId)
        {
            lock (_calls)
            {
                return _calls.Count(id => id == userId);
            }
        }

        public Task<SpotifyUserSession> ForUserAsync(Guid userId, CancellationToken ct)
        {
            lock (_calls)
            {
                _calls.Add(userId);
            }

            throw new SpotifyNotConnectedException();
        }
    }

    private sealed class DisconnectedFactory : ISpotifyClientFactory
    {
        public Task<SpotifyUserSession> ForUserAsync(Guid userId, CancellationToken ct) =>
            throw new SpotifyNotConnectedException();
    }
}
