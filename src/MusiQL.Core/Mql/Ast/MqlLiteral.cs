using MusiQL.Core.Mql.Schema;

namespace MusiQL.Core.Mql.Ast;

public abstract record MqlLiteral(TextSpan Span)
{
    public abstract MqlType Type { get; }
}

public sealed record StringLiteral(string Value, TextSpan Span) : MqlLiteral(Span)
{
    public override MqlType Type => MqlType.String;
}

public sealed record NumberLiteral(long Value, TextSpan Span) : MqlLiteral(Span)
{
    public override MqlType Type => MqlType.Number;
}
