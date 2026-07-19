namespace MusiQL.Core.Mql.Lexing;

public sealed record Token(TokenKind Kind, TextSpan Span, string Text, long Number = 0)
{
    public static string Describe(TokenKind kind) => kind switch
    {
        TokenKind.Identifier => "a field name",
        TokenKind.Number => "a number",
        TokenKind.String => "a quoted value",
        TokenKind.Where => "'where'",
        TokenKind.Order => "'order'",
        TokenKind.By => "'by'",
        TokenKind.Limit => "'limit'",
        TokenKind.And => "'and'",
        TokenKind.Or => "'or'",
        TokenKind.Not => "'not'",
        TokenKind.In => "'in'",
        TokenKind.Between => "'between'",
        TokenKind.Contains => "'contains'",
        TokenKind.Asc => "'asc'",
        TokenKind.Desc => "'desc'",
        TokenKind.From => "'from'",
        TokenKind.Library => "'library'",
        TokenKind.LeftParen => "'('",
        TokenKind.RightParen => "')'",
        TokenKind.Comma => "','",
        TokenKind.Equal => "'='",
        TokenKind.NotEqual => "'!='",
        TokenKind.Less => "'<'",
        TokenKind.LessEqual => "'<='",
        TokenKind.Greater => "'>'",
        TokenKind.GreaterEqual => "'>='",
        TokenKind.End => "end of query",
        _ => kind.ToString()
    };
}
