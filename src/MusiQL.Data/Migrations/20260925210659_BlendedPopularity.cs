using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusiQL.Data.Migrations
{
    /// <inheritdoc />
    public partial class BlendedPopularity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Recordings rank by a blended score: current Deezer rank where known,
            // ListenBrainz listeners (2005-2013 era Last.fm data) otherwise. The
            // column that the ranking indexes cover becomes that score (a rename,
            // so no table rewrite); the raw listener count moves to a new column.
            migrationBuilder.Sql("""
                ALTER TABLE catalog.recording_popularity RENAME COLUMN listeners TO score;
                ALTER TABLE catalog.recording_popularity ADD COLUMN IF NOT EXISTS listeners integer;
                ALTER TABLE catalog.recording_popularity ALTER COLUMN listens DROP NOT NULL;
                ALTER TABLE catalog.genre_top_recording RENAME COLUMN listeners TO score;

                CREATE TABLE IF NOT EXISTS catalog.recording_deezer (
                    recording_id bigint NOT NULL,
                    deezer_rank integer NOT NULL,
                    CONSTRAINT pk_recording_deezer PRIMARY KEY (recording_id)
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS catalog.recording_deezer;
                ALTER TABLE catalog.genre_top_recording RENAME COLUMN score TO listeners;
                ALTER TABLE catalog.recording_popularity DROP COLUMN IF EXISTS listeners;
                ALTER TABLE catalog.recording_popularity RENAME COLUMN score TO listeners;
                """);
        }
    }
}
