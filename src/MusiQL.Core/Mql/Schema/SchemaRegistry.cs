namespace MusiQL.Core.Mql.Schema;

public sealed class SchemaRegistry
{
    private readonly Dictionary<string, EntitySchema> _entities;

    public SchemaRegistry(IReadOnlyList<EntitySchema> entities) =>
        _entities = entities.ToDictionary(e => e.Name, StringComparer.Ordinal);

    public EntitySchema? Entity(string name) => _entities.GetValueOrDefault(name.ToLowerInvariant());

    public IReadOnlyCollection<string> EntityNames => _entities.Keys;
}
