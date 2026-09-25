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
    private bool _probedSelectiveGenre;

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

        var order = query.OrderBy.Count > 0
            ? query.OrderBy.Select(key => (Field: _entity.Field(key.Field.Name)!, key.Descending)).ToList()
            : _entity.DefaultOrderField is { } fallback
                ? [(Field: _entity.Field(fallback)!, Descending: true)]
                : [];

        var limit = EffectiveLimit(query, context);
        _parameters.Add(new MqlParameter(LimitParam, limit));

        var sql = RanksByPopularity(order) && RankedGenre(query.Where) is { } genre && _entity.GenreRank is { } rank
            ? GenreRanked(predicates, RankedPredicates(query, genre.Comparison), order[0].Field, rank, genre.Name)
            : RanksKnownFirst(order)
                ? KnownFirst(predicates, order[0].Field)
                : Plain(predicates, order);

        return new CompiledQuery(
            sql, _parameters, _entity.Name, _entity.ResultColumns, limit, query.FromLibrary);
    }

    private string Plain(List<string> predicates, List<(FieldSchema Field, bool Descending)> order)
    {
        var sql = new StringBuilder();
        sql.Append("SELECT ").Append(_entity.ProjectionSql).Append(" FROM ").Append(_entity.FromSql);
        if (predicates.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", predicates));
        }

        if (order.Count > 0)
        {
            sql.Append(" ORDER BY ").Append(string.Join(", ", order.Select(o => OrderKey(o.Field, o.Descending))));
        }

        return sql.Append(" LIMIT ").Append(LimitParam).ToString();
    }

    // Popularity lives on the optional side of a LEFT JOIN, so a plain ORDER BY
    // cannot walk its index and sorts every match instead (millions for a broad
    // genre). Split it: rows with a known value, taken in index order, then the
    // unknown ones. The outer LIMIT stops the append as soon as the first branch
    // fills it, so the second branch usually never runs.
    //
    // Not used when a small genre or the user's library already narrows the
    // rows: sorting that small set is cheaper than scanning the popularity index
    // for the few rows that match.
    private bool RanksKnownFirst(List<(FieldSchema Field, bool Descending)> order) =>
        RanksByPopularity(order) && !_probedSelectiveGenre;

    private bool RanksByPopularity(List<(FieldSchema Field, bool Descending)> order) =>
        order is [{ Descending: true, Field.NullsLast: true }] && !_fromLibrary;

    // A "genre = X" that every result must satisfy: a top-level conjunct, not
    // inside an OR or NOT. Only then can the precomputed ranking drive the query.
    private (string Name, ComparisonExpr Comparison)? RankedGenre(MqlExpr? where) => where switch
    {
        AndExpr and => RankedGenre(and.Left) ?? RankedGenre(and.Right),
        ComparisonExpr { Op: ComparisonOp.Equal, Value: StringLiteral name } comparison
            when _entity.Field(comparison.Field.Name)?.Kind == FieldKind.GenreMembership => (name.Value, comparison),
        _ => null
    };

    // The filter for the ranked branch, minus the genre test the ranking join
    // already guarantees. Left in, it makes the planner gather every member of
    // the genre instead of walking the ranking in order.
    private List<string> RankedPredicates(MqlQuery query, ComparisonExpr genre)
    {
        var predicates = new List<string>();
        if (Without(query.Where, genre) is { } rest)
        {
            predicates.Add(CompileExpr(rest));
        }

        return predicates;
    }

    private static MqlExpr? Without(MqlExpr? expr, MqlExpr target) => expr switch
    {
        null => null,
        _ when ReferenceEquals(expr, target) => null,
        AndExpr and => (Without(and.Left, target), Without(and.Right, target)) switch
        {
            (null, var right) => right,
            (var left, null) => left,
            (var left, var right) => and with { Left = left, Right = right }
        },
        _ => expr
    };

    // Read the genre's precomputed top tracks in rank order, applying the rest
    // of the filter as it goes; only if that runs out does the second branch
    // search the genre's remaining members (beyond the cap, or of unknown
    // popularity) the slow way. The outer LIMIT stops at whichever fills it.
    private string GenreRanked(
        List<string> predicates, List<string> rankedPredicates, FieldSchema rankField, GenreRank rank, string genre)
    {
        var name = AddParam(genre);
        var genreId = $"(SELECT g.id FROM catalog.genre g WHERE lower(g.name) = lower({name}))";
        var filter = string.Join(" AND ", predicates);
        var rankedFilter = rankedPredicates.Count > 0 ? " WHERE " + string.Join(" AND ", rankedPredicates) : "";

        var ranked =
            $"SELECT {_entity.ProjectionSql} FROM {_entity.FromSql} " +
            $"JOIN catalog.{rank.Table} gt ON gt.{rank.MemberColumn} = {rank.RootExpression} AND gt.genre_id = {genreId}" +
            $"{rankedFilter} ORDER BY gt.rank LIMIT {LimitParam}";
        var rest =
            $"SELECT {_entity.ProjectionSql} FROM {_entity.FromSql} WHERE {filter} AND NOT EXISTS (" +
            $"SELECT 1 FROM catalog.{rank.Table} gt WHERE gt.genre_id = {genreId} AND gt.{rank.MemberColumn} = {rank.RootExpression}) " +
            $"ORDER BY {rankField.SqlExpression} DESC NULLS LAST LIMIT {LimitParam}";

        return $"SELECT * FROM (({ranked}) UNION ALL ({rest})) ranked LIMIT {LimitParam}";
    }

    private string KnownFirst(List<string> predicates, FieldSchema rank)
    {
        var filter = predicates.Count > 0 ? string.Join(" AND ", predicates) + " AND " : "";
        var select = $"SELECT {_entity.ProjectionSql} FROM {_entity.FromSql} WHERE {filter}";
        return $"SELECT * FROM (" +
            $"({select}{rank.SqlExpression} IS NOT NULL ORDER BY {rank.SqlExpression} DESC LIMIT {LimitParam}) " +
            $"UNION ALL ({select}{rank.SqlExpression} IS NULL LIMIT {LimitParam})" +
            $") ranked LIMIT {LimitParam}";
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
    private static string OrderKey(FieldSchema field, bool descending) =>
        descending
            ? $"{field.SqlExpression} DESC{(field.NullsLast ? " NULLS LAST" : "")}"
            : $"{field.SqlExpression} ASC";

    private bool Selective(IEnumerable<string> lowered)
    {
        var selective = !_fromLibrary && lowered.All(_context.SelectiveGenres.Contains);
        _probedSelectiveGenre |= selective;
        return selective;
    }

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
