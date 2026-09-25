using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusiQL.Data.Migrations
{
    /// <inheritdoc />
    public partial class GenreTopRecording : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Each genre's most popular tracks, with membership inherited from the
            // album and artist already resolved and ranked by listeners. Lets
            // "tracks where genre = X" read one short index range instead of
            // gathering and sorting every member. Rebuilt by `etl rank`.
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS catalog.genre_top_recording (
                    genre_id bigint NOT NULL,
                    rank integer NOT NULL,
                    recording_id bigint NOT NULL,
                    listeners integer NOT NULL,
                    CONSTRAINT pk_genre_top_recording PRIMARY KEY (genre_id, rank)
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_genre_top_recording_member
                    ON catalog.genre_top_recording (genre_id, recording_id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS catalog.genre_top_recording;");
        }
    }
}
