namespace MusiQL.Core.Mql.Schema;

public sealed record GenreLink(string LinkTable, string ForeignKeyColumn, string RootExpression);

public sealed record ResultColumn(string Name, Type ClrType);

public sealed class EntitySchema
{
    private readonly Dictionary<string, FieldSchema> _fields;

    public EntitySchema(
        string name,
        string fromSql,
        string projectionSql,
        IReadOnlyList<ResultColumn> resultColumns,
        IReadOnlyList<FieldSchema> fields,
        IReadOnlyDictionary<string, string> aliases,
        IReadOnlyList<GenreLink> genreLinks,
        string librarySemiJoinSql)
    {
        Name = name;
        FromSql = fromSql;
        ProjectionSql = projectionSql;
        ResultColumns = resultColumns;
        GenreLinks = genreLinks;
        LibrarySemiJoinSql = librarySemiJoinSql;

        _fields = new Dictionary<string, FieldSchema>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            _fields[field.Name] = field;
        }

        foreach (var (alias, target) in aliases)
        {
            _fields[alias] = _fields[target];
        }
    }

    public string Name { get; }

    public string FromSql { get; }

    public string ProjectionSql { get; }

    public IReadOnlyList<ResultColumn> ResultColumns { get; }

    public IReadOnlyList<GenreLink> GenreLinks { get; }

    public string LibrarySemiJoinSql { get; }

    public FieldSchema? Field(string name) => _fields.GetValueOrDefault(name.ToLowerInvariant());

    public IReadOnlyCollection<string> FieldNames => _fields.Keys;
}
