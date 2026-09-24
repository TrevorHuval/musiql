using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusiQL.Data.Migrations
{
    /// <inheritdoc />
    public partial class CoveringGenreIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_release_group_genre_genre_id",
                schema: "catalog",
                table: "release_group_genre");

            migrationBuilder.DropIndex(
                name: "IX_recording_genre_genre_id",
                schema: "catalog",
                table: "recording_genre");

            migrationBuilder.DropIndex(
                name: "IX_artist_genre_genre_id",
                schema: "catalog",
                table: "artist_genre");

            migrationBuilder.CreateIndex(
                name: "IX_release_group_genre_genre_id_release_group_id",
                schema: "catalog",
                table: "release_group_genre",
                columns: new[] { "genre_id", "release_group_id" });

            migrationBuilder.CreateIndex(
                name: "IX_recording_genre_genre_id_recording_id",
                schema: "catalog",
                table: "recording_genre",
                columns: new[] { "genre_id", "recording_id" });

            migrationBuilder.CreateIndex(
                name: "IX_artist_genre_genre_id_artist_id",
                schema: "catalog",
                table: "artist_genre",
                columns: new[] { "genre_id", "artist_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_release_group_genre_genre_id_release_group_id",
                schema: "catalog",
                table: "release_group_genre");

            migrationBuilder.DropIndex(
                name: "IX_recording_genre_genre_id_recording_id",
                schema: "catalog",
                table: "recording_genre");

            migrationBuilder.DropIndex(
                name: "IX_artist_genre_genre_id_artist_id",
                schema: "catalog",
                table: "artist_genre");

            migrationBuilder.CreateIndex(
                name: "IX_release_group_genre_genre_id",
                schema: "catalog",
                table: "release_group_genre",
                column: "genre_id");

            migrationBuilder.CreateIndex(
                name: "IX_recording_genre_genre_id",
                schema: "catalog",
                table: "recording_genre",
                column: "genre_id");

            migrationBuilder.CreateIndex(
                name: "IX_artist_genre_genre_id",
                schema: "catalog",
                table: "artist_genre",
                column: "genre_id");
        }
    }
}
