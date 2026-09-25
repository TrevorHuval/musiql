using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusiQL.Data.Migrations
{
    /// <inheritdoc />
    public partial class PruneWeakGenreLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Same rule the ETL transform now applies on load (see transform.sql):
            // drop genre links holding under a quarter of the entity's top tag
            // votes, so an already-loaded catalog matches a fresh one.
            foreach (var (table, key) in new[]
            {
                ("artist_genre", "artist_id"),
                ("release_group_genre", "release_group_id"),
                ("recording_genre", "recording_id")
            })
            {
                migrationBuilder.Sql($"""
                    DELETE FROM catalog.{table} o
                    USING (SELECT {key}, max(votes) AS top FROM catalog.{table} GROUP BY {key}) t
                    WHERE t.{key} = o.{key} AND o.votes < 0.25 * t.top;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Pruned links come back with the next catalog load; nothing to undo here.
        }
    }
}
