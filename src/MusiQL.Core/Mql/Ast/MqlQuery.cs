namespace MusiQL.Core.Mql.Ast;

public sealed record MqlQuery(
    EntityRef Entity,
    bool FromLibrary,
    MqlExpr? Where,
    IReadOnlyList<OrderKey> OrderBy,
    int? Limit,
    TextSpan LimitSpan,
    TextSpan Span);

public sealed record EntityRef(string Name, TextSpan Span);

public sealed record FieldRef(string Name, TextSpan Span);

public sealed record OrderKey(FieldRef Field, bool Descending, TextSpan Span);
