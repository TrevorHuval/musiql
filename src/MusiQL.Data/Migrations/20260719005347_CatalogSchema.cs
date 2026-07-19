using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusiQL.Data.Migrations
{
    /// <inheritdoc />
    public partial class CatalogSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "artist",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    mbid = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    sort_name = table.Column<string>(type: "text", nullable: false),
                    begin_year = table.Column<short>(type: "smallint", nullable: true),
                    end_year = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artist", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "genre",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    mbid = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genre", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "release_group",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    mbid = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    artist_id = table.Column<long>(type: "bigint", nullable: false),
                    primary_type = table.Column<string>(type: "text", nullable: true),
                    first_release_year = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_release_group", x => x.id);
                    table.ForeignKey(
                        name: "FK_release_group_artist_artist_id",
                        column: x => x.artist_id,
                        principalSchema: "catalog",
                        principalTable: "artist",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "artist_genre",
                schema: "catalog",
                columns: table => new
                {
                    artist_id = table.Column<long>(type: "bigint", nullable: false),
                    genre_id = table.Column<long>(type: "bigint", nullable: false),
                    votes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artist_genre", x => new { x.artist_id, x.genre_id });
                    table.ForeignKey(
                        name: "FK_artist_genre_artist_artist_id",
                        column: x => x.artist_id,
                        principalSchema: "catalog",
                        principalTable: "artist",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_artist_genre_genre_genre_id",
                        column: x => x.genre_id,
                        principalSchema: "catalog",
                        principalTable: "genre",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recording",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    mbid = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    artist_id = table.Column<long>(type: "bigint", nullable: false),
                    length_ms = table.Column<int>(type: "integer", nullable: true),
                    release_group_id = table.Column<long>(type: "bigint", nullable: true),
                    first_release_year = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recording", x => x.id);
                    table.ForeignKey(
                        name: "FK_recording_artist_artist_id",
                        column: x => x.artist_id,
                        principalSchema: "catalog",
                        principalTable: "artist",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recording_release_group_release_group_id",
                        column: x => x.release_group_id,
                        principalSchema: "catalog",
                        principalTable: "release_group",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "release",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    mbid = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    release_group_id = table.Column<long>(type: "bigint", nullable: false),
                    artist_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_release", x => x.id);
                    table.ForeignKey(
                        name: "FK_release_artist_artist_id",
                        column: x => x.artist_id,
                        principalSchema: "catalog",
                        principalTable: "artist",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_release_release_group_release_group_id",
                        column: x => x.release_group_id,
                        principalSchema: "catalog",
                        principalTable: "release_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "release_group_genre",
                schema: "catalog",
                columns: table => new
                {
                    release_group_id = table.Column<long>(type: "bigint", nullable: false),
                    genre_id = table.Column<long>(type: "bigint", nullable: false),
                    votes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_release_group_genre", x => new { x.release_group_id, x.genre_id });
                    table.ForeignKey(
                        name: "FK_release_group_genre_genre_genre_id",
                        column: x => x.genre_id,
                        principalSchema: "catalog",
                        principalTable: "genre",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_release_group_genre_release_group_release_group_id",
                        column: x => x.release_group_id,
                        principalSchema: "catalog",
                        principalTable: "release_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recording_genre",
                schema: "catalog",
                columns: table => new
                {
                    recording_id = table.Column<long>(type: "bigint", nullable: false),
                    genre_id = table.Column<long>(type: "bigint", nullable: false),
                    votes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recording_genre", x => new { x.recording_id, x.genre_id });
                    table.ForeignKey(
                        name: "FK_recording_genre_genre_genre_id",
                        column: x => x.genre_id,
                        principalSchema: "catalog",
                        principalTable: "genre",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recording_genre_recording_recording_id",
                        column: x => x.recording_id,
                        principalSchema: "catalog",
                        principalTable: "recording",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_artist_begin_year",
                schema: "catalog",
                table: "artist",
                column: "begin_year");

            migrationBuilder.CreateIndex(
                name: "IX_artist_mbid",
                schema: "catalog",
                table: "artist",
                column: "mbid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_artist_name",
                schema: "catalog",
                table: "artist",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_artist_genre_genre_id",
                schema: "catalog",
                table: "artist_genre",
                column: "genre_id");

            migrationBuilder.CreateIndex(
                name: "IX_genre_mbid",
                schema: "catalog",
                table: "genre",
                column: "mbid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_genre_name",
                schema: "catalog",
                table: "genre",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recording_artist_id",
                schema: "catalog",
                table: "recording",
                column: "artist_id");

            migrationBuilder.CreateIndex(
                name: "IX_recording_first_release_year",
                schema: "catalog",
                table: "recording",
                column: "first_release_year");

            migrationBuilder.CreateIndex(
                name: "IX_recording_length_ms",
                schema: "catalog",
                table: "recording",
                column: "length_ms");

            migrationBuilder.CreateIndex(
                name: "IX_recording_mbid",
                schema: "catalog",
                table: "recording",
                column: "mbid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recording_name",
                schema: "catalog",
                table: "recording",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_recording_release_group_id",
                schema: "catalog",
                table: "recording",
                column: "release_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_recording_genre_genre_id",
                schema: "catalog",
                table: "recording_genre",
                column: "genre_id");

            migrationBuilder.CreateIndex(
                name: "IX_release_artist_id",
                schema: "catalog",
                table: "release",
                column: "artist_id");

            migrationBuilder.CreateIndex(
                name: "IX_release_mbid",
                schema: "catalog",
                table: "release",
                column: "mbid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_release_release_group_id",
                schema: "catalog",
                table: "release",
                column: "release_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_release_group_artist_id",
                schema: "catalog",
                table: "release_group",
                column: "artist_id");

            migrationBuilder.CreateIndex(
                name: "IX_release_group_first_release_year",
                schema: "catalog",
                table: "release_group",
                column: "first_release_year");

            migrationBuilder.CreateIndex(
                name: "IX_release_group_mbid",
                schema: "catalog",
                table: "release_group",
                column: "mbid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_release_group_name",
                schema: "catalog",
                table: "release_group",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_release_group_genre_genre_id",
                schema: "catalog",
                table: "release_group_genre",
                column: "genre_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "artist_genre",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "recording_genre",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "release",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "release_group_genre",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "recording",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "genre",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "release_group",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "artist",
                schema: "catalog");
        }
    }
}
