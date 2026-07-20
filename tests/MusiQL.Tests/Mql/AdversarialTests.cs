using MusiQL.Core.Mql;
using MusiQL.Core.Mql.Compilation;
using MusiQL.Core.Mql.Diagnostics;

namespace MusiQL.Tests.Mql;

public class AdversarialTests
{
    private static readonly MqlEngine Engine = MqlEngine.CreateDefault();

    [Theory]
    [InlineData("tracks where artist = \"x\"; drop table catalog.recording")]
    [InlineData("tracks where artist = \"x\" -- comment")]
    [InlineData("tracks where genre = \"x\" /* comment */")]
    [InlineData("tracks'; drop table catalog.artist")]
    [InlineData("tracks where 1 = 1")]
    [InlineData("tracks where artist = \"x\" union select mbid from catalog.artist")]
    [InlineData("tracks where artist = 'x'")]
    [InlineData("tracks where year = 1990 or 1=1")]
    [InlineData("tracks where pg_sleep(10) > 0")]
    [InlineData("tracks where year = 1990); drop table catalog.artist --")]
    [InlineData("tracks order by (select mbid from catalog.artist)")]
    [InlineData("tracks order by year; delete from catalog.artist")]
    [InlineData("tracks where artist = \"x\" \\g")]
    [InlineData("tracks where year = 0x1F")]
    [InlineData("tracks limit 10 offset 5")]
    [InlineData("tracks where artist = `x`")]
    public void Injection_attempts_never_compile(string input)
    {
        Assert.False(Engine.Compile(input, CompileContext.Default).Success);
    }

    [Theory]
    [InlineData("tracks where year = 99999999999999999999")]
    [InlineData("tracks limit 99999999999999999999")]
    public void Out_of_range_numbers_fail_cleanly(string input)
    {
        var result = Engine.Compile(input, CompileContext.Default);
        Assert.False(result.Success);
        Assert.Equal(MqlErrorCode.NumberOutOfRange, result.Errors[0].Code);
    }

    [Fact]
    public void From_library_without_a_user_is_rejected()
    {
        var result = Engine.Compile("tracks from library", CompileContext.Default);
        Assert.False(result.Success);
        Assert.Equal(MqlErrorCode.LibraryUserRequired, result.Errors[0].Code);
    }

    [Fact]
    public void Homoglyph_entity_never_compiles()
    {
        var input = (char)0x0430 + "rtists where year = 1990";
        Assert.False(Engine.Compile(input, CompileContext.Default).Success);
    }

    [Fact]
    public void Homoglyph_field_never_compiles()
    {
        var input = "tracks where " + (char)0x0430 + "rtist = \"Nirvana\"";
        Assert.False(Engine.Compile(input, CompileContext.Default).Success);
    }

    [Theory]
    [InlineData("tracks where artist = \"'; DROP TABLE catalog.recording; --\"", "'; DROP TABLE catalog.recording; --")]
    [InlineData("tracks where genre contains \"; delete from catalog.artist\"", "; delete from catalog.artist")]
    [InlineData("tracks where artist = \"Bob\\\" or \\\"1\\\"=\\\"1\"", "Bob\" or \"1\"=\"1")]
    [InlineData("albums where title = \"a) OR (SELECT 1\"", "a) OR (SELECT 1")]
    public void Malicious_string_values_become_inert_parameters(string input, string payload)
    {
        var result = Engine.Compile(input, CompileContext.Default);
        Assert.True(result.Success);

        Assert.DoesNotContain("DROP", result.Query!.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("delete", result.Query.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(payload, result.Query.Sql, StringComparison.Ordinal);
        Assert.Contains(result.Query.Parameters, p => p.Value is string s && s.Contains(payload, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_literal_is_a_bind_parameter()
    {
        var result = Engine.Compile(
            "tracks where genre in (\"a\", \"b\") and year between 1 and 2 and artist contains \"c\"",
            CompileContext.Default);
        Assert.True(result.Success);

        // No bare numeric or quoted literals survive into the SQL: every value is @p* or @row_limit.
        Assert.DoesNotContain('"', result.Query!.Sql);
        Assert.DoesNotContain("= 1", result.Query.Sql);
        Assert.DoesNotContain("BETWEEN 1", result.Query.Sql);
    }
}
