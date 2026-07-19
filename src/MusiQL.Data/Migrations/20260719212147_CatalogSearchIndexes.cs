using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusiQL.Data.Migrations
{
    /// <inheritdoc />
    public partial class CatalogSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE INDEX ix_artist_name_lower ON catalog.artist (lower(name) text_pattern_ops);");
            migrationBuilder.Sql(
                "CREATE INDEX ix_genre_name_lower ON catalog.genre (lower(name) text_pattern_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS catalog.ix_artist_name_lower;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS catalog.ix_genre_name_lower;");
        }
    }
}
