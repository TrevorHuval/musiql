using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MusiQL.Data.App;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Playlist> Playlists => Set<Playlist>();
    public DbSet<PlaylistSnapshot> PlaylistSnapshots => Set<PlaylistSnapshot>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SpotifyAccount> SpotifyAccounts => Set<SpotifyAccount>();
    public DbSet<SpotifyAuthState> SpotifyAuthStates => Set<SpotifyAuthState>();
    public DbSet<SpotifyPlaylistLink> SpotifyPlaylistLinks => Set<SpotifyPlaylistLink>();
    public DbSet<SpotifySavedTrack> SpotifySavedTracks => Set<SpotifySavedTrack>();
    public DbSet<TrackMatch> TrackMatches => Set<TrackMatch>();
    public DbSet<RecordingIsrc> RecordingIsrcs => Set<RecordingIsrc>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.HasDefaultSchema("app");

        model.Entity<AppUser>().ToTable("users");
        model.Entity<IdentityRole<Guid>>().ToTable("roles");
        model.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        model.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        model.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        model.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        model.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");

        model.Entity<Playlist>(e =>
        {
            e.ToTable("playlist");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).ValueGeneratedNever();
            e.Property(p => p.Name).HasMaxLength(Playlist.MaxNameLength).IsRequired();
            e.Property(p => p.Description).HasMaxLength(Playlist.MaxDescriptionLength);
            e.Property(p => p.MqlText).HasMaxLength(Playlist.MaxMqlLength).IsRequired();
            e.HasIndex(p => new { p.OwnerId, p.Name });
            e.HasOne<AppUser>().WithMany().HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<PlaylistSnapshot>(e =>
        {
            e.ToTable("playlist_snapshot");
            e.HasKey(s => s.PlaylistId);
            e.Property(s => s.PlaylistId).ValueGeneratedNever();
            e.Property(s => s.MqlText).HasMaxLength(Playlist.MaxMqlLength).IsRequired();
            e.Property(s => s.Payload).HasColumnType("jsonb").IsRequired();
            e.HasOne<Playlist>().WithMany().HasForeignKey(s => s.PlaylistId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_token");
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).ValueGeneratedNever();
            e.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasIndex(t => t.UserId);
            e.HasOne<AppUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<SpotifyAccount>(e =>
        {
            e.ToTable("spotify_account");
            e.HasKey(a => a.UserId);
            e.Property(a => a.UserId).ValueGeneratedNever();
            e.Property(a => a.SpotifyUserId).HasMaxLength(128).IsRequired();
            e.Property(a => a.DisplayName).HasMaxLength(256);
            e.Property(a => a.AccessTokenCipher).IsRequired();
            e.Property(a => a.RefreshTokenCipher).IsRequired();
            e.Property(a => a.Scopes).HasMaxLength(512);
            e.HasOne<AppUser>().WithOne().HasForeignKey<SpotifyAccount>(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<SpotifyAuthState>(e =>
        {
            e.ToTable("spotify_auth_state");
            e.HasKey(s => s.State);
            e.Property(s => s.State).HasMaxLength(128).ValueGeneratedNever();
            e.Property(s => s.CodeVerifier).HasMaxLength(256).IsRequired();
            e.Property(s => s.RedirectUri).HasMaxLength(512).IsRequired();
            e.HasIndex(s => s.ExpiresAt);
            e.HasOne<AppUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<SpotifyPlaylistLink>(e =>
        {
            e.ToTable("spotify_playlist_link");
            e.HasKey(l => l.PlaylistId);
            e.Property(l => l.PlaylistId).ValueGeneratedNever();
            e.Property(l => l.SpotifyPlaylistId).HasMaxLength(128).IsRequired();
            e.HasIndex(l => l.UserId);
            e.HasOne<Playlist>().WithMany().HasForeignKey(l => l.PlaylistId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<SpotifySavedTrack>(e =>
        {
            e.ToTable("spotify_saved_track");
            e.HasKey(t => new { t.UserId, t.SpotifyTrackId });
            e.Property(t => t.SpotifyTrackId).HasMaxLength(128);
            e.Property(t => t.Isrc).HasMaxLength(32);
            e.Property(t => t.Title).HasMaxLength(1000).IsRequired();
            e.Property(t => t.Artist).HasMaxLength(1000).IsRequired();
            e.HasIndex(t => t.UserId);
            e.HasOne<AppUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<TrackMatch>(e =>
        {
            e.ToTable("track_match");
            e.HasKey(m => m.RecordingMbid);
            e.Property(m => m.RecordingMbid).ValueGeneratedNever();
            e.Property(m => m.SpotifyTrackId).HasMaxLength(128);
            e.Property(m => m.SpotifyUri).HasMaxLength(256);
            e.Property(m => m.Isrc).HasMaxLength(32);
            e.Property(m => m.Method).HasMaxLength(16).IsRequired();
        });

        model.Entity<RecordingIsrc>(e =>
        {
            e.ToTable("recording_isrc");
            e.HasKey(r => r.RecordingMbid);
            e.Property(r => r.RecordingMbid).ValueGeneratedNever();
            e.Property(r => r.Isrc).HasMaxLength(256);
        });

        foreach (var entity in model.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    private static string ToSnakeCase(string name) =>
        Regex.Replace(name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
}
