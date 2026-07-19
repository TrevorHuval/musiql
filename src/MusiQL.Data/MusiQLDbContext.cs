using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MusiQL.Data.Catalog;

namespace MusiQL.Data;

public class MusiQLDbContext(DbContextOptions<MusiQLDbContext> options) : DbContext(options)
{
    public DbSet<Artist> Artists => Set<Artist>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<ReleaseGroup> ReleaseGroups => Set<ReleaseGroup>();
    public DbSet<Release> Releases => Set<Release>();
    public DbSet<Recording> Recordings => Set<Recording>();
    public DbSet<ArtistGenre> ArtistGenres => Set<ArtistGenre>();
    public DbSet<ReleaseGroupGenre> ReleaseGroupGenres => Set<ReleaseGroupGenre>();
    public DbSet<RecordingGenre> RecordingGenres => Set<RecordingGenre>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("catalog");

        model.Entity<Artist>(e =>
        {
            e.Property(a => a.Id).ValueGeneratedNever();
            e.HasIndex(a => a.Mbid).IsUnique();
            e.HasIndex(a => a.Name);
            e.HasIndex(a => a.BeginYear);
        });

        model.Entity<Genre>(e =>
        {
            e.Property(g => g.Id).ValueGeneratedNever();
            e.HasIndex(g => g.Mbid).IsUnique();
            e.HasIndex(g => g.Name).IsUnique();
        });

        model.Entity<ReleaseGroup>(e =>
        {
            e.Property(rg => rg.Id).ValueGeneratedNever();
            e.HasIndex(rg => rg.Mbid).IsUnique();
            e.HasIndex(rg => rg.Name);
            e.HasIndex(rg => rg.FirstReleaseYear);
            e.HasIndex(rg => rg.ArtistId);
            e.HasOne(rg => rg.Artist).WithMany().HasForeignKey(rg => rg.ArtistId);
        });

        model.Entity<Release>(e =>
        {
            e.Property(r => r.Id).ValueGeneratedNever();
            e.HasIndex(r => r.Mbid).IsUnique();
            e.HasIndex(r => r.ReleaseGroupId);
            e.HasOne(r => r.ReleaseGroup).WithMany().HasForeignKey(r => r.ReleaseGroupId);
            e.HasOne(r => r.Artist).WithMany().HasForeignKey(r => r.ArtistId);
        });

        model.Entity<Recording>(e =>
        {
            e.Property(r => r.Id).ValueGeneratedNever();
            e.HasIndex(r => r.Mbid).IsUnique();
            e.HasIndex(r => r.Name);
            e.HasIndex(r => r.FirstReleaseYear);
            e.HasIndex(r => r.LengthMs);
            e.HasIndex(r => r.ArtistId);
            e.HasIndex(r => r.ReleaseGroupId);
            e.HasOne(r => r.Artist).WithMany().HasForeignKey(r => r.ArtistId);
            e.HasOne(r => r.ReleaseGroup).WithMany().HasForeignKey(r => r.ReleaseGroupId);
        });

        model.Entity<ArtistGenre>(e =>
        {
            e.HasKey(ag => new { ag.ArtistId, ag.GenreId });
            e.HasIndex(ag => ag.GenreId);
            e.HasOne(ag => ag.Artist).WithMany(a => a.Genres).HasForeignKey(ag => ag.ArtistId);
            e.HasOne(ag => ag.Genre).WithMany().HasForeignKey(ag => ag.GenreId);
        });

        model.Entity<ReleaseGroupGenre>(e =>
        {
            e.HasKey(rgg => new { rgg.ReleaseGroupId, rgg.GenreId });
            e.HasIndex(rgg => rgg.GenreId);
            e.HasOne(rgg => rgg.ReleaseGroup).WithMany(rg => rg.Genres).HasForeignKey(rgg => rgg.ReleaseGroupId);
            e.HasOne(rgg => rgg.Genre).WithMany().HasForeignKey(rgg => rgg.GenreId);
        });

        model.Entity<RecordingGenre>(e =>
        {
            e.HasKey(rg => new { rg.RecordingId, rg.GenreId });
            e.HasIndex(rg => rg.GenreId);
            e.HasOne(rg => rg.Recording).WithMany(r => r.Genres).HasForeignKey(rg => rg.RecordingId);
            e.HasOne(rg => rg.Genre).WithMany().HasForeignKey(rg => rg.GenreId);
        });

        foreach (var entity in model.Model.GetEntityTypes())
        {
            entity.SetTableName(ToSnakeCase(entity.ClrType.Name));
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    private static string ToSnakeCase(string name) =>
        Regex.Replace(name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
}
