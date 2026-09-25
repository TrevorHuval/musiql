using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusiQL.Data.Migrations
{
    /// <inheritdoc />
    public partial class Popularity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ListenBrainz listener counts, fetched by `etl popularity`. Kept apart
            // from the entity tables so a catalog refresh (TRUNCATE + reload) does
            // not wipe them; MusicBrainz ids are stable across dumps. Only rows
            // with at least one listener are stored. listeners rides in the key
            // index so sorting by popularity is an index-only lookup.
            foreach (var (table, key) in new[]
            {
                ("recording_popularity", "recording_id"),
                ("release_group_popularity", "release_group_id"),
                ("artist_popularity", "artist_id")
            })
            {
                migrationBuilder.Sql($"""
                    CREATE TABLE IF NOT EXISTS catalog.{table} (
                        {key} bigint NOT NULL,
                        listeners integer NOT NULL,
                        listens bigint NOT NULL,
                        CONSTRAINT pk_{table} PRIMARY KEY ({key}) INCLUDE (listeners)
                    );
                    CREATE INDEX IF NOT EXISTS ix_{table}_rank
                        ON catalog.{table} (listeners DESC NULLS LAST, {key});
                    CREATE INDEX IF NOT EXISTS ix_{table}_rank ON catalog.{table} (listeners DESC NULLS LAST, {key});
                    """);
            }

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS catalog.popularity_progress (
                    entity text PRIMARY KEY,
                    last_id bigint NOT NULL,
                    updated_at timestamptz NOT NULL DEFAULT now()
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TABLE IF EXISTS catalog.recording_popularity, catalog.release_group_popularity, " +
                "catalog.artist_popularity, catalog.popularity_progress;");
        }
    }
}
