using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusiQL.Data.Migrations
{
    /// <inheritdoc />
    public partial class RecordingArtistTitleIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Spotify library sync matches thousands of (artist, title) pairs in one
            // query; without this it walks every recording of each matched artist.
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_recording_artist_lower_name ON catalog.recording (artist_id, lower(name));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS catalog.ix_recording_artist_lower_name;");
        }
    }
}
