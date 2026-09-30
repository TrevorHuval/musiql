using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusiQL.Data.Migrations.App
{
    /// <inheritdoc />
    public partial class SpotifyPlaylistLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "spotify_playlist_link_track",
                schema: "app",
                columns: table => new
                {
                    playlist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    spotify_uri = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spotify_playlist_link_track", x => new { x.playlist_id, x.spotify_uri });
                    table.ForeignKey(
                        name: "FK_spotify_playlist_link_track_spotify_playlist_link_playlist_~",
                        column: x => x.playlist_id,
                        principalSchema: "app",
                        principalTable: "spotify_playlist_link",
                        principalColumn: "playlist_id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "spotify_playlist_link_track",
                schema: "app");
        }
    }
}
