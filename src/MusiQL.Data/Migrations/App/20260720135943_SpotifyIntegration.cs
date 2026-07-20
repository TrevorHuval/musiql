using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusiQL.Data.Migrations.App
{
    /// <inheritdoc />
    public partial class SpotifyIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recording_isrc",
                schema: "app",
                columns: table => new
                {
                    recording_mbid = table.Column<Guid>(type: "uuid", nullable: false),
                    isrc = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fetched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recording_isrc", x => x.recording_mbid);
                });

            migrationBuilder.CreateTable(
                name: "spotify_account",
                schema: "app",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    spotify_user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    display_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    access_token_cipher = table.Column<string>(type: "text", nullable: false),
                    refresh_token_cipher = table.Column<string>(type: "text", nullable: false),
                    scopes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    access_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    connected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    library_synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    library_saved_count = table.Column<int>(type: "integer", nullable: false),
                    library_matched_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spotify_account", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_spotify_account_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "spotify_auth_state",
                schema: "app",
                columns: table => new
                {
                    state = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_verifier = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    redirect_uri = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spotify_auth_state", x => x.state);
                    table.ForeignKey(
                        name: "FK_spotify_auth_state_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "spotify_playlist_link",
                schema: "app",
                columns: table => new
                {
                    playlist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    spotify_playlist_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    track_count = table.Column<int>(type: "integer", nullable: false),
                    last_exported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spotify_playlist_link", x => x.playlist_id);
                    table.ForeignKey(
                        name: "FK_spotify_playlist_link_playlist_playlist_id",
                        column: x => x.playlist_id,
                        principalSchema: "app",
                        principalTable: "playlist",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "spotify_saved_track",
                schema: "app",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    spotify_track_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    isrc = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    artist = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    duration_ms = table.Column<int>(type: "integer", nullable: true),
                    added_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recording_id = table.Column<long>(type: "bigint", nullable: true),
                    recording_mbid = table.Column<Guid>(type: "uuid", nullable: true),
                    confidence = table.Column<double>(type: "double precision", nullable: true),
                    synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spotify_saved_track", x => new { x.user_id, x.spotify_track_id });
                    table.ForeignKey(
                        name: "FK_spotify_saved_track_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "track_match",
                schema: "app",
                columns: table => new
                {
                    recording_mbid = table.Column<Guid>(type: "uuid", nullable: false),
                    spotify_track_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    spotify_uri = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    isrc = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    confidence = table.Column<double>(type: "double precision", nullable: false),
                    method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    matched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_track_match", x => x.recording_mbid);
                });

            migrationBuilder.CreateIndex(
                name: "IX_spotify_auth_state_expires_at",
                schema: "app",
                table: "spotify_auth_state",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_spotify_auth_state_user_id",
                schema: "app",
                table: "spotify_auth_state",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_spotify_playlist_link_user_id",
                schema: "app",
                table: "spotify_playlist_link",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_spotify_saved_track_user_id",
                schema: "app",
                table: "spotify_saved_track",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recording_isrc",
                schema: "app");

            migrationBuilder.DropTable(
                name: "spotify_account",
                schema: "app");

            migrationBuilder.DropTable(
                name: "spotify_auth_state",
                schema: "app");

            migrationBuilder.DropTable(
                name: "spotify_playlist_link",
                schema: "app");

            migrationBuilder.DropTable(
                name: "spotify_saved_track",
                schema: "app");

            migrationBuilder.DropTable(
                name: "track_match",
                schema: "app");
        }
    }
}
