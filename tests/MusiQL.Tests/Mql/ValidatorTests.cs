using MusiQL.Core.Mql;
using MusiQL.Core.Mql.Compilation;
using MusiQL.Core.Mql.Diagnostics;

namespace MusiQL.Tests.Mql;

public class ValidatorTests
{
    private static readonly MqlEngine Engine = MqlEngine.CreateDefault();

    [Fact]
    public void Canonical_query_is_valid()
    {
        var result = Compile("tracks where genre = \"grunge\" and year between 1990 and 2004 and artist != \"Nirvana\"");
        Assert.True(result.Success);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Unknown_entity_is_rejected()
    {
        var error = SingleError("playlists where year = 1990");
        Assert.Equal(MqlErrorCode.UnknownEntity, error.Code);
        Assert.Equal(0, error.Span.Start);
    }

    [Fact]
    public void Unknown_field_is_rejected_with_position()
    {
        var input = "tracks where colour = \"blue\"";
        var error = SingleError(input);
        Assert.Equal(MqlErrorCode.UnknownField, error.Code);
        Assert.Equal(input.IndexOf("colour", StringComparison.Ordinal), error.Span.Start);
    }

    [Theory]
    [InlineData("tracks where artist > \"x\"")]
    [InlineData("tracks where year contains \"9\"")]
    [InlineData("albums where genre < \"x\"")]
    public void Operator_not_allowed_is_rejected(string input)
    {
        Assert.Equal(MqlErrorCode.OperatorNotAllowed, SingleError(input).Code);
    }

    [Theory]
    [InlineData("tracks where year = \"soon\"")]
    [InlineData("tracks where artist = 5")]
    [InlineData("tracks where year between \"a\" and \"b\"")]
    public void Type_mismatch_is_rejected(string input)
    {
        var result = Compile(input);
        Assert.False(result.Success);
        Assert.All(result.Errors, e => Assert.Equal(MqlErrorCode.TypeMismatch, e.Code));
    }

    [Fact]
    public void Order_by_unknown_field_is_rejected()
    {
        Assert.Equal(MqlErrorCode.UnknownField, SingleError("tracks order by loudness").Code);
    }

    [Fact]
    public void Order_by_genre_is_rejected()
    {
        Assert.Equal(MqlErrorCode.OperatorNotAllowed, SingleError("tracks order by genre").Code);
    }

    [Fact]
    public void Duplicate_order_field_is_rejected()
    {
        Assert.Equal(MqlErrorCode.DuplicateOrderField, SingleError("tracks order by year, year desc").Code);
    }

    [Fact]
    public void Zero_limit_is_rejected()
    {
        Assert.Equal(MqlErrorCode.LimitOutOfRange, SingleError("tracks limit 0").Code);
    }

    [Fact]
    public void Field_scoped_to_other_entity_is_rejected()
    {
        Assert.Equal(MqlErrorCode.UnknownField, SingleError("artists where album = \"x\"").Code);
        Assert.Equal(MqlErrorCode.UnknownField, SingleError("albums where length > 100").Code);
    }

    [Fact]
    public void Aliases_resolve()
    {
        Assert.True(Compile("tracks where track contains \"love\" order by rating desc").Success);
        Assert.True(Compile("artists where artist = \"Nirvana\"").Success);
    }

    [Fact]
    public void From_library_requires_a_user()
    {
        Assert.Equal(MqlErrorCode.LibraryUserRequired, SingleError("tracks from library").Code);
        Assert.True(Compile("tracks from library", Guid.NewGuid()).Success);
    }

    [Fact]
    public void Too_many_predicates_are_rejected()
    {
        var clauses = Enumerable.Repeat("year = 1990", MqlLimits.MaxPredicates + 1);
        var input = "tracks where " + string.Join(" and ", clauses);
        Assert.Equal(MqlErrorCode.TooManyClauses, SingleError(input).Code);
    }

    [Fact]
    public void Oversized_in_list_is_rejected()
    {
        var items = string.Join(", ", Enumerable.Range(1, MqlLimits.MaxInListItems + 1));
        Assert.Equal(MqlErrorCode.TooManyClauses, SingleError($"tracks where year in ({items})").Code);
    }

    private static MqlCompilation Compile(string input, Guid? userId = null) =>
        Engine.Compile(input, new CompileContext { CallerUserId = userId });

    private static MqlError SingleError(string input, Guid? userId = null)
    {
        var result = Compile(input, userId);
        Assert.False(result.Success);
        return Assert.Single(result.Errors);
    }
}
