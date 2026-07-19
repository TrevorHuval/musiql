namespace MusiQL.Api.Contracts;

public sealed record Suggestion(Guid Mbid, string Name);

public sealed record SchemaResponse(IReadOnlyList<EntityMetadata> Entities);

public sealed record EntityMetadata(
    string Name,
    bool SupportsLibrary,
    IReadOnlyList<FieldMetadata> Fields,
    IReadOnlyList<QueryColumn> Columns);

public sealed record FieldMetadata(
    string Name,
    string Type,
    bool Orderable,
    IReadOnlyList<string> Operators,
    IReadOnlyList<string> Aliases);
