namespace MusiQL.Core.Mql.Ast;

public abstract record MqlExpr(TextSpan Span);

public sealed record AndExpr(MqlExpr Left, MqlExpr Right, TextSpan Span) : MqlExpr(Span);

public sealed record OrExpr(MqlExpr Left, MqlExpr Right, TextSpan Span) : MqlExpr(Span);

public sealed record NotExpr(MqlExpr Operand, TextSpan Span) : MqlExpr(Span);

public sealed record ComparisonExpr(FieldRef Field, ComparisonOp Op, MqlLiteral Value, TextSpan Span)
    : MqlExpr(Span);

public sealed record InExpr(FieldRef Field, IReadOnlyList<MqlLiteral> Values, TextSpan Span)
    : MqlExpr(Span);

public sealed record BetweenExpr(FieldRef Field, MqlLiteral Low, MqlLiteral High, TextSpan Span)
    : MqlExpr(Span);

public sealed record ContainsExpr(FieldRef Field, StringLiteral Value, TextSpan Span) : MqlExpr(Span);

public enum ComparisonOp
{
    Equal,
    NotEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual
}
