using MusiQL.Etl.Loading;
using MusiQL.Etl.Tsv;

namespace MusiQL.Tests.Etl;

public class MbdumpParsingTests
{
    [Fact]
    public void Null_marker_is_recognised()
    {
        Assert.True(TsvValue.IsNull(@"\N"));
        Assert.False(TsvValue.IsNull(""));
        Assert.False(TsvValue.IsNull(@"\Nope"));
        Assert.False(TsvValue.IsNull("N"));
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData(@"tab\there", "tab\there")]
    [InlineData(@"line\nbreak", "line\nbreak")]
    [InlineData(@"back\\slash", @"back\slash")]
    [InlineData(@"carriage\rreturn", "carriage\rreturn")]
    public void Escapes_are_decoded(string field, string expected)
    {
        Assert.Equal(expected, TsvValue.Unescape(field));
    }

    [Fact]
    public void Field_returns_null_for_null_marker_and_out_of_range()
    {
        var fields = Mbdump.SplitFields("a\t\\N\tc");
        Assert.Equal("a", Mbdump.Field(fields, 0));
        Assert.Null(Mbdump.Field(fields, 1));
        Assert.Equal("c", Mbdump.Field(fields, 2));
        Assert.Null(Mbdump.Field(fields, 9));
    }

    [Fact]
    public void Artist_row_projects_to_the_columns_we_keep()
    {
        var spec = TableSpecs.All.Single(s => s.Table == "artist");
        var line = string.Join('\t',
            "42", "c0b2500e-0cf0-44d0-9a20-000000000042", "Soundgarden", "Soundgarden",
            "1984", @"\N", @"\N", @"\N", @"\N", @"\N", @"\N", @"\N", @"\N", "", "0",
            @"\N", "f", @"\N", @"\N");
        var fields = Mbdump.SplitFields(line);

        Assert.Equal("42", Field(spec, fields, "id"));
        Assert.Equal("c0b2500e-0cf0-44d0-9a20-000000000042", Field(spec, fields, "gid"));
        Assert.Equal("Soundgarden", Field(spec, fields, "name"));
        Assert.Equal("1984", Field(spec, fields, "begin_year"));
        Assert.Null(Field(spec, fields, "end_year"));
    }

    [Fact]
    public void Track_row_projects_recording_and_medium()
    {
        var spec = TableSpecs.All.Single(s => s.Table == "track");
        var line = string.Join('\t',
            "7", "6adba250-1c84-5590-ad3a-9d24d0387f94", "700", "300", "1", "1",
            "Alive", "1002", "341000", "0", @"\N", "f");
        var fields = Mbdump.SplitFields(line);

        Assert.Equal("700", Field(spec, fields, "recording"));
        Assert.Equal("300", Field(spec, fields, "medium"));
    }

    private static string? Field(TableSpec spec, string[] fields, string column) =>
        Mbdump.Field(fields, spec.Columns.Single(c => c.Name == column).Source);
}
