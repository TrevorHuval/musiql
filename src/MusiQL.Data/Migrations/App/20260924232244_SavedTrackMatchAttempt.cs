using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusiQL.Data.Migrations.App
{
    /// <inheritdoc />
    public partial class SavedTrackMatchAttempt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "match_attempted_at",
                schema: "app",
                table: "spotify_saved_track",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "match_attempted_at",
                schema: "app",
                table: "spotify_saved_track");
        }
    }
}
