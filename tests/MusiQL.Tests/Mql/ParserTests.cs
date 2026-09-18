using MusiQL.Core.Mql;
using MusiQL.Core.Mql.Ast;
using MusiQL.Core.Mql.Diagnostics;
using MusiQL.Core.Mql.Parsing;

namespace MusiQL.Tests.Mql;

public class ParserTests
{
    [Fact]
    public void Parses_entity_only()
    {
        var query = Parser.Parse("tracks");
        Assert.Equal("tracks", query.Entity.Name);
        Assert.Null(query.Where);
        Assert.False(query.FromLibrary);
        Assert.Empty(query.OrderBy);
        Assert.Null(query.Limit);
    }

    [Fact]
    public void Parses_from_library()
    {
        var query = Parser.Parse("tracks from library where year = 1994");
        Assert.True(query.FromLibrary);
        Assert.IsType<ComparisonExpr>(query.Where);
    }

    [Fact]
    public void And_is_left_associative_and_binds_tighter_than_or()
    {
        var query = Parser.Parse("tracks where year = 1 or year = 2 and year = 3");
        var or = Assert.IsType<OrExpr>(query.Where);
        Assert.IsType<ComparisonExpr>(or.Left);
        Assert.IsType<AndExpr>(or.Right);
    }

    [Fact]
    public void Parentheses_override_precedence()
    {
        var query = Parser.Parse("tracks where (year = 1 or year = 2) and year = 3");
        var and = Assert.IsType<AndExpr>(query.Where);
        Assert.IsType<OrExpr>(and.Left);
    }

    [Fact]
    public void Parses_not_prefix()
    {
        var query = Parser.Parse("tracks where not genre = \"pop\"");
        var not = Assert.IsType<NotExpr>(query.Where);
        Assert.IsType<ComparisonExpr>(not.Operand);
    }

    [Fact]
    public void Parses_in_list()
    {
        var query = Parser.Parse("tracks where genre in (\"grunge\", \"punk\")");
        var inExpr = Assert.IsType<InExpr>(query.Where);
        Assert.Equal(2, inExpr.Values.Count);
    }

    [Fact]
    public void Parses_between()
    {
        var query = Parser.Parse("tracks where year between 1990 and 2004");
        var between = Assert.IsType<BetweenExpr>(query.Where);
        Assert.Equal(1990, Assert.IsType<NumberLiteral>(between.Low).Value);
        Assert.Equal(2004, Assert.IsType<NumberLiteral>(between.High).Value);
    }

    [Fact]
    public void Parses_order_by_multiple_keys()
    {
        var query = Parser.Parse("tracks order by year desc, artist");
        Assert.Equal(2, query.OrderBy.Count);
        Assert.True(query.OrderBy[0].Descending);
        Assert.False(query.OrderBy[1].Descending);
    }

    [Fact]
    public void Parses_limit()
    {
        var query = Parser.Parse("tracks limit 25");
        Assert.Equal(25, query.Limit);
    }

    [Fact]
    public void Missing_value_reports_position_at_end()
    {
        var input = "tracks where year =";
        var error = Parse(input);
        Assert.Equal(MqlErrorCode.UnexpectedToken, error.Code);
        Assert.Equal(input.Length, error.Span.Start);
    }

    [Fact]
    public void Trailing_tokens_are_rejected()
    {
        var error = Parse("tracks where year = 1990 artist = \"x\"");
        Assert.Equal(MqlErrorCode.UnexpectedToken, error.Code);
        Assert.Equal("tracks where year = 1990 ".Length, error.Span.Start);
    }

    [Fact]
    public void Missing_operator_reports_expected_hints()
    {
        var error = Parse("tracks where year");
        Assert.Equal(MqlErrorCode.UnexpectedToken, error.Code);
        Assert.Contains("'in'", error.Expected!);
        Assert.Contains("'between'", error.Expected!);
    }

    [Fact]
    public void From_without_library_is_rejected()
    {
        var error = Parse("tracks from where year = 1");
        Assert.Equal(MqlErrorCode.UnexpectedToken, error.Code);
    }

    [Fact]
    public void Deeply_nested_parentheses_are_rejected()
    {
        var input = "tracks where " + new string('(', MqlLimits.MaxExpressionDepth + 1);
        var error = Parse(input);
        Assert.Equal(MqlErrorCode.ExpressionTooDeep, error.Code);
    }

    [Fact]
    public void Deep_not_chain_is_rejected()
    {
        var chain = string.Concat(Enumerable.Repeat("not ", MqlLimits.MaxExpressionDepth + 1));
        var error = Parse("tracks where " + chain + "genre = \"x\"");
        Assert.Equal(MqlErrorCode.ExpressionTooDeep, error.Code);
    }

    [Fact]
    public void Source_longer_than_the_limit_is_rejected_before_lexing()
    {
        var input = "tracks where " + new string(' ', MqlLimits.MaxSourceLength) + "year = 1";
        var error = Parse(input);
        Assert.Equal(MqlErrorCode.QueryTooLong, error.Code);
        Assert.Equal(MqlLimits.MaxSourceLength, error.Span.Start);
    }

    [Fact]
    public void Token_budget_is_enforced()
    {
        var input = "tracks where " + string.Concat(Enumerable.Repeat("(", MqlLimits.MaxTokens + 1));
        var error = Parse(input);
        Assert.Equal(MqlErrorCode.QueryTooLong, error.Code);
    }

    private static MqlError Parse(string input) =>
        Assert.Throws<MqlException>(() => Parser.Parse(input)).Error;
}
