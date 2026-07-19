namespace MusiQL.Core.Mql.Lexing;

public enum TokenKind
{
    Identifier,
    Number,
    String,

    Where,
    Order,
    By,
    Limit,
    And,
    Or,
    Not,
    In,
    Between,
    Contains,
    Asc,
    Desc,
    From,
    Library,

    LeftParen,
    RightParen,
    Comma,
    Equal,
    NotEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,

    End
}
