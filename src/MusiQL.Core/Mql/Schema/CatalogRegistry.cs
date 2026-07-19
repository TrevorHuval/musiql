namespace MusiQL.Core.Mql.Schema;

public static class CatalogRegistry
{
    private static readonly MqlOperator[] StringOps =
        [MqlOperator.Equal, MqlOperator.NotEqual, MqlOperator.In, MqlOperator.Contains];

    private static readonly MqlOperator[] NumberOps =
    [
        MqlOperator.Equal, MqlOperator.NotEqual, MqlOperator.Less, MqlOperator.LessEqual,
        MqlOperator.Greater, MqlOperator.GreaterEqual, MqlOperator.In, MqlOperator.Between
    ];

    public static SchemaRegistry Create() => new([Tracks(), Albums(), Artists()]);

    private static EntitySchema Tracks() => new(
        name: "tracks",
        fromSql:
            "catalog.recording r " +
            "JOIN catalog.artist a ON a.id = r.artist_id " +
            "LEFT JOIN catalog.release_group rg ON rg.id = r.release_group_id",
        projectionSql:
            "r.mbid AS id, r.name AS title, a.name AS artist, rg.name AS album, " +
            "r.first_release_year AS year, r.length_ms AS length_ms",
        resultColumns:
        [
            new ResultColumn("id", typeof(Guid)),
            new ResultColumn("title", typeof(string)),
            new ResultColumn("artist", typeof(string)),
            new ResultColumn("album", typeof(string)),
            new ResultColumn("year", typeof(short?)),
            new ResultColumn("length_ms", typeof(int?))
        ],
        fields:
        [
            new FieldSchema("title", FieldKind.StringScalar, StringOps, "r.name"),
            new FieldSchema("artist", FieldKind.StringScalar, StringOps, "a.name"),
            new FieldSchema("album", FieldKind.StringScalar, StringOps, "rg.name"),
            new FieldSchema("genre", FieldKind.GenreMembership, StringOps),
            new FieldSchema("year", FieldKind.NumberScalar, NumberOps, "r.first_release_year"),
            new FieldSchema("decade", FieldKind.NumberScalar, NumberOps, "((r.first_release_year / 10) * 10)"),
            new FieldSchema("length", FieldKind.NumberScalar, NumberOps, "(r.length_ms / 1000)"),
            new FieldSchema("votes", FieldKind.NumberScalar, NumberOps,
                "(SELECT max(g.votes) FROM catalog.recording_genre g WHERE g.recording_id = r.id)")
        ],
        aliases: new Dictionary<string, string>
        {
            ["track"] = "title",
            ["name"] = "title",
            ["rating"] = "votes"
        },
        genreLinks:
        [
            new GenreLink("recording_genre", "recording_id", "r.id"),
            new GenreLink("release_group_genre", "release_group_id", "r.release_group_id"),
            new GenreLink("artist_genre", "artist_id", "r.artist_id")
        ],
        librarySemiJoinSql:
            "SELECT 1 FROM app.user_library ul WHERE ul.recording_id = r.id AND ul.user_id = @caller_user_id");

    private static EntitySchema Albums() => new(
        name: "albums",
        fromSql:
            "catalog.release_group rg " +
            "JOIN catalog.artist a ON a.id = rg.artist_id",
        projectionSql:
            "rg.mbid AS id, rg.name AS title, a.name AS artist, rg.primary_type AS type, " +
            "rg.first_release_year AS year",
        resultColumns:
        [
            new ResultColumn("id", typeof(Guid)),
            new ResultColumn("title", typeof(string)),
            new ResultColumn("artist", typeof(string)),
            new ResultColumn("type", typeof(string)),
            new ResultColumn("year", typeof(short?))
        ],
        fields:
        [
            new FieldSchema("title", FieldKind.StringScalar, StringOps, "rg.name"),
            new FieldSchema("artist", FieldKind.StringScalar, StringOps, "a.name"),
            new FieldSchema("type", FieldKind.StringScalar, StringOps, "rg.primary_type"),
            new FieldSchema("genre", FieldKind.GenreMembership, StringOps),
            new FieldSchema("year", FieldKind.NumberScalar, NumberOps, "rg.first_release_year"),
            new FieldSchema("decade", FieldKind.NumberScalar, NumberOps, "((rg.first_release_year / 10) * 10)"),
            new FieldSchema("votes", FieldKind.NumberScalar, NumberOps,
                "(SELECT max(g.votes) FROM catalog.release_group_genre g WHERE g.release_group_id = rg.id)")
        ],
        aliases: new Dictionary<string, string>
        {
            ["album"] = "title",
            ["name"] = "title",
            ["rating"] = "votes"
        },
        genreLinks:
        [
            new GenreLink("release_group_genre", "release_group_id", "rg.id"),
            new GenreLink("artist_genre", "artist_id", "rg.artist_id")
        ],
        librarySemiJoinSql:
            "SELECT 1 FROM app.user_library ul " +
            "JOIN catalog.recording rec ON rec.id = ul.recording_id " +
            "WHERE rec.release_group_id = rg.id AND ul.user_id = @caller_user_id");

    private static EntitySchema Artists() => new(
        name: "artists",
        fromSql: "catalog.artist a",
        projectionSql: "a.mbid AS id, a.name AS name, a.begin_year AS year",
        resultColumns:
        [
            new ResultColumn("id", typeof(Guid)),
            new ResultColumn("name", typeof(string)),
            new ResultColumn("year", typeof(short?))
        ],
        fields:
        [
            new FieldSchema("name", FieldKind.StringScalar, StringOps, "a.name"),
            new FieldSchema("genre", FieldKind.GenreMembership, StringOps),
            new FieldSchema("year", FieldKind.NumberScalar, NumberOps, "a.begin_year"),
            new FieldSchema("decade", FieldKind.NumberScalar, NumberOps, "((a.begin_year / 10) * 10)"),
            new FieldSchema("votes", FieldKind.NumberScalar, NumberOps,
                "(SELECT max(g.votes) FROM catalog.artist_genre g WHERE g.artist_id = a.id)")
        ],
        aliases: new Dictionary<string, string>
        {
            ["artist"] = "name",
            ["rating"] = "votes"
        },
        genreLinks:
        [
            new GenreLink("artist_genre", "artist_id", "a.id")
        ],
        librarySemiJoinSql:
            "SELECT 1 FROM app.user_library ul " +
            "JOIN catalog.recording rec ON rec.id = ul.recording_id " +
            "WHERE rec.artist_id = a.id AND ul.user_id = @caller_user_id");
}
