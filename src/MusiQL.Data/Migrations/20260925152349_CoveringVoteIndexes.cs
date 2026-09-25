using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusiQL.Data.Migrations
{
    /// <inheritdoc />
    public partial class CoveringVoteIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "order by votes" reads max(votes) for every candidate row. Carrying
            // votes in the index makes that an index-only scan instead of a heap
            // fetch per row, which is most of the cost of a cold query.
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_recording_genre_votes ON catalog.recording_genre (recording_id, votes);");
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_release_group_genre_votes ON catalog.release_group_genre (release_group_id, votes);");
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_artist_genre_votes ON catalog.artist_genre (artist_id, votes);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS catalog.ix_recording_genre_votes;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS catalog.ix_release_group_genre_votes;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS catalog.ix_artist_genre_votes;");
        }
    }
}
