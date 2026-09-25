using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusiQL.Data.Migrations.App
{
    /// <inheritdoc />
    public partial class PlaylistKeepLive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "keep_live",
                schema: "app",
                table: "spotify_playlist_link",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_refresh_attempt_at",
                schema: "app",
                table: "spotify_playlist_link",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_refresh_error",
                schema: "app",
                table: "spotify_playlist_link",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_spotify_playlist_link_keep_live",
                schema: "app",
                table: "spotify_playlist_link",
                column: "keep_live",
                filter: "keep_live");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_spotify_playlist_link_keep_live",
                schema: "app",
                table: "spotify_playlist_link");

            migrationBuilder.DropColumn(
                name: "keep_live",
                schema: "app",
                table: "spotify_playlist_link");

            migrationBuilder.DropColumn(
                name: "last_refresh_attempt_at",
                schema: "app",
                table: "spotify_playlist_link");

            migrationBuilder.DropColumn(
                name: "last_refresh_error",
                schema: "app",
                table: "spotify_playlist_link");
        }
    }
}
