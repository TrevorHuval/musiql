using System.Text;
using MusiQL.Core.Mql.Ast;
using MusiQL.Core.Mql.Schema;

namespace MusiQL.Core.Mql.Compilation;

public sealed class SqlCompiler
{
    private const string UserParam = "@caller_user_id";
    private const string LimitParam = "@row_limit";

    private readonly EntitySchema _entity;
    private readonly CompileContext _context;
    private readonly List<MqlParameter> _parameters = [];
    private int _next;
    private bool _fromLibrary;

    private SqlCompiler(EntitySchema entity, CompileContext context)
    {
        _entity = entity;
        _context = context;
    }

    public static CompiledQuery Compile(MqlQuery query, SchemaRegistry registry, CompileContext context)
    {
        var entity = registry.Entity(query.Entity.Name)
            ?? throw new InvalidOperationException($"Entity '{query.Entity.Name}' is not in the registry.");
        return new SqlCompiler(entity, context).Build(query, context);
    }

    private CompiledQuery Build(MqlQuery query, CompileContext context)
    {
        var sql = new StringBuilder();
        sql.Append("SELECT ").Append(_entity.ProjectionSql);
        sql.Append(" FROM ").Append(_entity.FromSql);

        var predicates = new List<string>();
        _fromLibrary = query.FromLibrary;
        if (query.FromLibrary)
        {
            _parameters.Add(new MqlParameter(UserParam, RequireUserId(context)));
            predicates.Add($"EXISTS ({_entity.LibrarySemiJoinSql})");
        }

        if (query.Where is not null)
        {
            predicates.Add(CompileExpr(query.Where));
        }

        if (predicates.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", predicates));
        }

        if (query.OrderBy.Count > 0)
        {
            var keys = query.OrderBy.Select(key =>
            {
                var expr = _entity.Field(key.Field.Name)!.SqlExpression;
                return $"{expr}{(key.Descending ? " DESC" : " ASC")}";
            });
            sql.Append(" ORDER BY ").Append(string.Join(", ", keys));
        }

        var limit = EffectiveLimit(query, context);
        _parameters.Add(new MqlParameter(LimitParam, limit));
        sql.Append(" LIMIT ").Append(LimitParam);

        return new CompiledQuery(
            sql.ToString(), _parameters, _entity.Name, _entity.ResultColumns, limit, query.FromLibrary);
    }

    private string CompileExpr(MqlExpr expr) => expr switch
    {
        AndExpr and => $"({CompileExpr(and.Left)} AND {CompileExpr(and.Right)})",
        OrExpr or => $"({CompileExpr(or.Left)} OR {CompileExpr(or.Right)})",
        NotExpr not => $"(NOT {CompileExpr(not.Operand)})",
        ComparisonExpr comparison => CompileComparison(comparison),
        InExpr inExpr => CompileIn(inExpr),
        BetweenExpr between => CompileBetween(between),
        ContainsExpr contains => CompileContains(contains),
        _ => throw new InvalidOperationException($"Cannot compile {expr.GetType().Name}.")
    };

    private string CompileComparison(ComparisonExpr comparison)
    {
        var field = _entity.Field(comparison.Field.Name)!;
        if (field.Kind == FieldKind.GenreMembership)
        {
            return CompileGenreEquality((StringLiteral)comparison.Value, comparison.Op == ComparisonOp.NotEqual);
        }

        if (field.Kind == FieldKind.StringScalar)
        {
            var param = AddParam(((StringLiteral)comparison.Value).Value);
            var op = comparison.Op == ComparisonOp.Equal ? "=" : "<>";
            return $"lower({field.SqlExpression}) {op} lower({param})";
        }

        var numberParam = AddParam(((NumberLiteral)comparison.Value).Value);
        return $"{field.SqlExpression} {CompareOp(comparison.Op)} {numberParam}";
    }

    private string CompileIn(InExpr inExpr)
    {
        var field = _entity.Field(inExpr.Field.Name)!;
        if (field.Kind == FieldKind.GenreMembership)
        {
            var lowered = inExpr.Values.Select(v => ((StringLiteral)v).Value.ToLowerInvariant()).ToArray();
            var param = AddParam(lowered);
            return GenreMembership($"lower(g.name) = ANY({param})", negate: false, Selective(lowered));
        }

        if (field.Kind == FieldKind.StringScalar)
        {
            var lowered = inExpr.Values.Select(v => ((StringLiteral)v).Value.ToLowerInvariant()).ToArray();
            var param = AddParam(lowered);
            return $"lower({field.SqlExpression}) = ANY({param})";
        }

        var numbers = inExpr.Values.Select(v => ((NumberLiteral)v).Value).ToArray();
        var numberParam = AddParam(numbers);
        return $"{field.SqlExpression} = ANY({numberParam})";
    }

    private string CompileBetween(BetweenExpr between)
    {
        var field = _entity.Field(between.Field.Name)!;
        var low = AddParam(((NumberLiteral)between.Low).Value);
        var high = AddParam(((NumberLiteral)between.High).Value);
        return $"{field.SqlExpression} BETWEEN {low} AND {high}";
    }

    private string CompileContains(ContainsExpr contains)
    {
        var field = _entity.Field(contains.Field.Name)!;
        var pattern = "%" + EscapeLike(contains.Value.Value) + "%";
        var param = AddParam(pattern);
        if (field.Kind == FieldKind.GenreMembership)
        {
            return GenreMembership($"g.name ILIKE {param} ESCAPE '\\'", negate: false);
        }

        return $"{field.SqlExpression} ILIKE {param} ESCAPE '\\'";
    }

    private string CompileGenreEquality(StringLiteral value, bool negate)
    {
        var param = AddParam(value.Value);
        return GenreMembership(
            $"lower(g.name) = lower({param})", negate, Selective([value.Value.ToLowerInvariant()]));
    }

    // A library holds a few thousand tracks, so the library semi-join is the
    // cheapest place to start and genre is best checked per row from there. The
    // id-probe shape would instead bitmap every member of the genre first.
    private bool Selective(IEnumerable<string> lowered) =>
        !_fromLibrary && lowered.All(_context.SelectiveGenres.Contains);

    // Two shapes for the same membership test. A small genre resolves each
    // level's ids once and probes indexes with them, which is what a query with
    // no other filter needs. A broad genre keeps correlated EXISTS, which the
    // planner turns into a hashed filter behind a year or artist predicate;
    // materializing a million-id array there would be far slower.
    private string GenreMembership(string namePredicate, bool negate, bool selective = false)
    {
        var levels = selective && !negate
            ? _entity.GenreLinks.Select(link =>
                $"{link.RootExpression} = ANY(ARRAY(SELECT lg.{link.ForeignKeyColumn} " +
                $"FROM catalog.{link.LinkTable} lg JOIN catalog.genre g ON g.id = lg.genre_id " +
                $"WHERE {namePredicate}))")
            : _entity.GenreLinks.Select(link =>
                $"EXISTS (SELECT 1 FROM catalog.{link.LinkTable} lg " +
                $"JOIN catalog.genre g ON g.id = lg.genre_id " +
                $"WHERE lg.{link.ForeignKeyColumn} = {link.RootExpression} AND {namePredicate})");
        var membership = $"({string.Join(" OR ", levels)})";
        return negate ? $"(NOT {membership})" : membership;
    }

    private Guid RequireUserId(CompileContext context) =>
        context.CallerUserId
        ?? throw new InvalidOperationException("'from library' requires a caller user id.");

    private static int EffectiveLimit(MqlQuery query, CompileContext context)
    {
        var requested = query.Limit ?? MqlLimits.DefaultRowCap;
        return Math.Clamp(requested, 1, context.RowCap);
    }

    private string AddParam(object value)
    {
        var name = $"@p{_next++}";
        _parameters.Add(new MqlParameter(name, value));
        return name;
    }

    private static string CompareOp(ComparisonOp op) => op switch
    {
        ComparisonOp.Equal => "=",
        ComparisonOp.NotEqual => "<>",
        ComparisonOp.Less => "<",
        ComparisonOp.LessEqual => "<=",
        ComparisonOp.Greater => ">",
        ComparisonOp.GreaterEqual => ">=",
        _ => throw new ArgumentOutOfRangeException(nameof(op))
    };

    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_");
}
