namespace MusiQL.Etl.Loading;

public static class TableSpecs
{
    public static readonly IReadOnlyList<TableSpec> All =
    [
        new("artist",
        [
            new("id", 0, FieldType.Int),
            new("gid", 1, FieldType.Uuid),
            new("name", 2, FieldType.Text),
            new("sort_name", 3, FieldType.Text),
            new("begin_year", 4, FieldType.Short),
            new("end_year", 7, FieldType.Short)
        ]),
        new("artist_credit_name",
        [
            new("artist_credit", 0, FieldType.Int),
            new("position", 1, FieldType.Short),
            new("artist", 2, FieldType.Int)
        ]),
        new("release_group",
        [
            new("id", 0, FieldType.Int),
            new("gid", 1, FieldType.Uuid),
            new("name", 2, FieldType.Text),
            new("artist_credit", 3, FieldType.Int),
            new("type", 4, FieldType.Int)
        ]),
        new("release_group_meta",
        [
            new("id", 0, FieldType.Int),
            new("first_release_year", 2, FieldType.Short)
        ]),
        new("release_group_primary_type",
        [
            new("id", 0, FieldType.Int),
            new("name", 1, FieldType.Text)
        ]),
        new("release",
        [
            new("id", 0, FieldType.Int),
            new("gid", 1, FieldType.Uuid),
            new("name", 2, FieldType.Text),
            new("artist_credit", 3, FieldType.Int),
            new("release_group", 4, FieldType.Int),
            new("status", 5, FieldType.Int)
        ]),
        new("release_status",
        [
            new("id", 0, FieldType.Int),
            new("name", 1, FieldType.Text)
        ]),
        new("recording",
        [
            new("id", 0, FieldType.Int),
            new("gid", 1, FieldType.Uuid),
            new("name", 2, FieldType.Text),
            new("artist_credit", 3, FieldType.Int),
            new("length", 4, FieldType.Int)
        ]),
        new("medium",
        [
            new("id", 0, FieldType.Int),
            new("release", 1, FieldType.Int)
        ]),
        new("track",
        [
            new("recording", 2, FieldType.Int),
            new("medium", 3, FieldType.Int)
        ]),
        new("genre",
        [
            new("id", 0, FieldType.Int),
            new("gid", 1, FieldType.Uuid),
            new("name", 2, FieldType.Text)
        ]),
        new("tag",
        [
            new("id", 0, FieldType.Int),
            new("name", 1, FieldType.Text)
        ]),
        new("artist_tag",
        [
            new("artist", 0, FieldType.Int),
            new("tag", 1, FieldType.Int),
            new("count", 2, FieldType.Int)
        ]),
        new("release_group_tag",
        [
            new("release_group", 0, FieldType.Int),
            new("tag", 1, FieldType.Int),
            new("count", 2, FieldType.Int)
        ]),
        new("recording_tag",
        [
            new("recording", 0, FieldType.Int),
            new("tag", 1, FieldType.Int),
            new("count", 2, FieldType.Int)
        ])
    ];
}
