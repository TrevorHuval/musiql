using MusiQL.Core.Mql.Diagnostics;
using MusiQL.Core.Mql.Lexing;

namespace MusiQL.Tests.Mql;

public class LexerTests
{
    [Fact]
    public void Tokenizes_a_full_query()
    {
        var kinds = Tokenize("tracks where year >= 1990 order by rating desc limit 20")
            .Select(t => t.Kind)
            .ToArray();

        Assert.Equal(
        [
            TokenKind.Identifier, TokenKind.Where, TokenKind.Identifier, TokenKind.GreaterEqual,
            TokenKind.Number, TokenKind.Order, TokenKind.By, TokenKind.Identifier, TokenKind.Desc,
            TokenKind.Limit, TokenKind.Number, TokenKind.End
        ], kinds);
    }

    [Fact]
    public void Keywords_are_case_insensitive()
    {
        var kinds = Tokenize("Tracks WHERE genre CONTAINS \"rock\"").Select(t => t.Kind).ToArray();
        Assert.Equal(
        [
            TokenKind.Identifier, TokenKind.Where, TokenKind.Identifier, TokenKind.Contains,
            TokenKind.String, TokenKind.End
        ], kinds);
    }

    [Fact]
    public void Decodes_string_escapes()
    {
        var token = Tokenize("\"a\\\"b\\\\c\"")[0];
        Assert.Equal(TokenKind.String, token.Kind);
        Assert.Equal("a\"b\\c", token.Text);
    }

    [Fact]
    public void Parses_number_value()
    {
        var token = Tokenize("2004")[0];
        Assert.Equal(TokenKind.Number, token.Kind);
        Assert.Equal(2004, token.Number);
    }

    [Theory]
    [InlineData("year = 1990; drop table", 11)]
    [InlineData("artist = 'x'", 9)]
    [InlineData("year = 1 -- comment", 9)]
    [InlineData("genre = \"a\" /* c */", 12)]
    [InlineData("year = 1 % 2", 9)]
    public void Rejects_characters_outside_the_grammar(string input, int position)
    {
        var error = Assert.Throws<MqlException>(() => new Lexer(input).Tokenize()).Error;
        Assert.Equal(MqlErrorCode.UnexpectedCharacter, error.Code);
        Assert.Equal(position, error.Span.Start);
    }

    [Fact]
    public void Rejects_unicode_homoglyph_identifiers()
    {
        // Cyrillic 'а' (U+0430) standing in for Latin 'a' in "artist".
        var homoglyph = (char)0x0430 + "rtist = \"x\"";
        var error = Assert.Throws<MqlException>(() => new Lexer(homoglyph).Tokenize()).Error;
        Assert.Equal(MqlErrorCode.UnexpectedCharacter, error.Code);
        Assert.Equal(0, error.Span.Start);
    }

    [Fact]
    public void Rejects_unterminated_string()
    {
        var error = Assert.Throws<MqlException>(() => new Lexer("artist = \"open").Tokenize()).Error;
        Assert.Equal(MqlErrorCode.UnterminatedString, error.Code);
    }

    [Fact]
    public void Rejects_unknown_escape()
    {
        var error = Assert.Throws<MqlException>(() => new Lexer("\"a\\xb\"").Tokenize()).Error;
        Assert.Equal(MqlErrorCode.InvalidEscape, error.Code);
    }

    [Fact]
    public void Rejects_lone_bang()
    {
        var error = Assert.Throws<MqlException>(() => new Lexer("year ! 1").Tokenize()).Error;
        Assert.Equal(MqlErrorCode.UnexpectedCharacter, error.Code);
    }

    private static IReadOnlyList<Token> Tokenize(string input) => new Lexer(input).Tokenize();
}
