using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusiQL.Data.Migrations.App
{
    /// <inheritdoc />
    public partial class PlaylistSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "playlist_snapshot",
                schema: "app",
                columns: table => new
                {
                    playlist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mql_text = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    computed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playlist_snapshot", x => x.playlist_id);
                    table.ForeignKey(
                        name: "FK_playlist_snapshot_playlist_playlist_id",
                        column: x => x.playlist_id,
                        principalSchema: "app",
                        principalTable: "playlist",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "playlist_snapshot",
                schema: "app");
        }
    }
}
