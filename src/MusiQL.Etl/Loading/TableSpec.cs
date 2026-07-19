namespace MusiQL.Etl.Loading;

public enum FieldType
{
    Int,
    Short,
    Uuid,
    Text
}

public record Column(string Name, int Source, FieldType Type);

public record TableSpec(string Table, IReadOnlyList<Column> Columns)
{
    public string ColumnList => string.Join(", ", Columns.Select(c => c.Name));
}
