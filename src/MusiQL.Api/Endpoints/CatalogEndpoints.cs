using Microsoft.EntityFrameworkCore;
using MusiQL.Api.Contracts;
using MusiQL.Api.Query;
using MusiQL.Core.Mql;
using MusiQL.Core.Mql.Schema;
using MusiQL.Data;

namespace MusiQL.Api.Endpoints;

public static class CatalogEndpoints
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 50;

    public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/genres", Genres).RequireRateLimiting(RateLimits.Query);
        group.MapGet("/artists", Artists).RequireRateLimiting(RateLimits.Query);
        group.MapGet("/fields", Fields);
        return group;
    }

    private static async Task<IResult> Genres(string? q, int? limit, MusiQLDbContext db, CancellationToken ct)
    {
        var take = Clamp(limit);
        var query = string.IsNullOrWhiteSpace(q)
            ? db.Genres.OrderBy(g => g.Name)
            : db.Genres.Where(g => EF.Functions.Like(g.Name.ToLower(), Prefix(q))).OrderBy(g => g.Name);

        var results = await query
            .Take(take)
            .Select(g => new Suggestion(g.Mbid, g.Name))
            .ToListAsync(ct);

        return Results.Ok(results);
    }

    private static async Task<IResult> Artists(string? q, int? limit, MusiQLDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Results.Ok(Array.Empty<Suggestion>());
        }

        var results = await db.Artists
            .Where(a => EF.Functions.Like(a.Name.ToLower(), Prefix(q)))
            .OrderBy(a => a.Name)
            .Take(Clamp(limit))
            .Select(a => new Suggestion(a.Mbid, a.Name))
            .ToListAsync(ct);

        return Results.Ok(results);
    }

    private static IResult Fields(MqlEngine engine)
    {
        var registry = engine.Registry;
        var entities = registry.EntityNames
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => Describe(registry.Entity(name)!))
            .ToList();

        return Results.Ok(new SchemaResponse(entities));
    }

    private static EntityMetadata Describe(EntitySchema entity)
    {
        var fields = entity.FieldNames
            .Select(fieldName => entity.Field(fieldName)!)
            .Distinct()
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .Select(f => new FieldMetadata(
                f.Name,
                FieldType(f.Kind),
                f.Orderable,
                f.Operators.OrderBy(o => o).Select(MqlOperatorText.Describe).ToList(),
                Aliases(entity, f)))
            .ToList();

        var columns = entity.ResultColumns
            .Select(c => new QueryColumn(c.Name, ColumnTypes.Of(c.ClrType)))
            .ToList();

        return new EntityMetadata(entity.Name, SupportsLibrary: true, fields, columns);
    }

    private static IReadOnlyList<string> Aliases(EntitySchema entity, FieldSchema field) =>
        entity.FieldNames
            .Where(name => ReferenceEquals(entity.Field(name), field) && !string.Equals(name, field.Name, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    private static string FieldType(FieldKind kind) => kind switch
    {
        FieldKind.NumberScalar => "number",
        FieldKind.GenreMembership => "genre",
        _ => "string"
    };

    private static int Clamp(int? limit) => Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

    private static string Prefix(string q) =>
        q.Trim().ToLowerInvariant().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}
