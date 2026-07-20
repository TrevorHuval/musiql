using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusiQL.Data.Migrations
{
    /// <inheritdoc />
    public partial class TrigramSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql(
                "CREATE INDEX ix_recording_name_trgm ON catalog.recording USING gin (name gin_trgm_ops);");
            migrationBuilder.Sql(
                "CREATE INDEX ix_artist_name_trgm ON catalog.artist USING gin (name gin_trgm_ops);");
            migrationBuilder.Sql(
                "CREATE INDEX ix_release_group_name_trgm ON catalog.release_group USING gin (name gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS catalog.ix_recording_name_trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS catalog.ix_artist_name_trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS catalog.ix_release_group_name_trgm;");
        }
    }
}
