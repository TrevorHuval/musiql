using MusiQL.Core.Mql;
using MusiQL.Core.Mql.Compilation;

namespace MusiQL.Tests.Mql;

public class CompilerTests
{
    private static readonly MqlEngine Engine = MqlEngine.CreateDefault();

    [Fact]
    public void Selective_genre_probes_ids_and_negation_stays_correlated()
    {
        var context = new CompileContext { SelectiveGenres = new HashSet<string> { "acid house" } };

        var probe = Engine.Compile("albums where genre = \"Acid House\"", context).Query!.Sql;
        Assert.Contains("rg.id = ANY(ARRAY(SELECT lg.release_group_id", probe);
        Assert.Contains("rg.artist_id = ANY(ARRAY(SELECT lg.artist_id", probe);
        Assert.DoesNotContain("EXISTS", probe);

        var negated = Engine.Compile("albums where genre != \"acid house\"", context).Query!.Sql;
        Assert.Contains("NOT (EXISTS", negated);

        var broad = Engine.Compile("albums where genre = \"rock\"", context).Query!.Sql;
        Assert.Contains("EXISTS", broad);

        var library = Engine.Compile(
            "tracks from library where genre = \"acid house\"",
            context with { CallerUserId = Guid.NewGuid() }).Query!.Sql;
        Assert.DoesNotContain("ANY(ARRAY", library);
    }

    [Fact]
    public void Numeric_comparison()
    {
        var snapshot = Compile("tracks where year >= 1990 limit 10");
        Assert.Equal(
            """
            SELECT r.mbid AS id, r.name AS title, a.name AS artist, rg.name AS album, r.first_release_year AS year, r.length_ms AS length_ms FROM catalog.recording r JOIN catalog.artist a ON a.id = r.artist_id LEFT JOIN catalog.release_group rg ON rg.id = r.release_group_id WHERE r.first_release_year >= @p0 LIMIT @row_limit
            --
            @p0 = bigint 1990
            @row_limit = int 10
            """,
            snapshot);
    }

    [Fact]
    public void String_equality_is_case_insensitive()
    {
        var snapshot = Compile("artists where artist = \"Nirvana\"");
        Assert.Equal(
            """
            SELECT a.mbid AS id, a.name AS name, a.begin_year AS year FROM catalog.artist a WHERE lower(a.name) = lower(@p0) LIMIT @row_limit
            --
            @p0 = text "Nirvana"
            @row_limit = int 100
            """,
            snapshot);
    }

    [Fact]
    public void Contains_escapes_wildcards_into_a_bound_pattern()
    {
        var snapshot = Compile("tracks where artist contains \"50%_off\"");
        Assert.Equal(
            """
            SELECT r.mbid AS id, r.name AS title, a.name AS artist, rg.name AS album, r.first_release_year AS year, r.length_ms AS length_ms FROM catalog.recording r JOIN catalog.artist a ON a.id = r.artist_id LEFT JOIN catalog.release_group rg ON rg.id = r.release_group_id WHERE a.name ILIKE @p0 ESCAPE '\' LIMIT @row_limit
            --
            @p0 = text "%50\%\_off%"
            @row_limit = int 100
            """,
            snapshot);
    }

    [Fact]
    public void Genre_in_spans_album_and_artist_levels()
    {
        var snapshot = Compile("albums where genre in (\"Grunge\", \"Punk\")");
        Assert.Equal(
            """
            SELECT rg.mbid AS id, rg.name AS title, a.name AS artist, rg.primary_type AS type, rg.first_release_year AS year FROM catalog.release_group rg JOIN catalog.artist a ON a.id = rg.artist_id WHERE (EXISTS (SELECT 1 FROM catalog.release_group_genre lg JOIN catalog.genre g ON g.id = lg.genre_id WHERE lg.release_group_id = rg.id AND lower(g.name) = ANY(@p0)) OR EXISTS (SELECT 1 FROM catalog.artist_genre lg JOIN catalog.genre g ON g.id = lg.genre_id WHERE lg.artist_id = rg.artist_id AND lower(g.name) = ANY(@p0))) LIMIT @row_limit
            --
            @p0 = text[] ["grunge", "punk"]
            @row_limit = int 100
            """,
            snapshot);
    }

    [Fact]
    public void Genre_inequality_negates_membership_across_all_levels()
    {
        var snapshot = Compile("tracks where genre != \"pop\"");
        Assert.Equal(
            """
            SELECT r.mbid AS id, r.name AS title, a.name AS artist, rg.name AS album, r.first_release_year AS year, r.length_ms AS length_ms FROM catalog.recording r JOIN catalog.artist a ON a.id = r.artist_id LEFT JOIN catalog.release_group rg ON rg.id = r.release_group_id WHERE (NOT (EXISTS (SELECT 1 FROM catalog.recording_genre lg JOIN catalog.genre g ON g.id = lg.genre_id WHERE lg.recording_id = r.id AND lower(g.name) = lower(@p0)) OR EXISTS (SELECT 1 FROM catalog.release_group_genre lg JOIN catalog.genre g ON g.id = lg.genre_id WHERE lg.release_group_id = r.release_group_id AND lower(g.name) = lower(@p0)) OR EXISTS (SELECT 1 FROM catalog.artist_genre lg JOIN catalog.genre g ON g.id = lg.genre_id WHERE lg.artist_id = r.artist_id AND lower(g.name) = lower(@p0)))) LIMIT @row_limit
            --
            @p0 = text "pop"
            @row_limit = int 100
            """,
            snapshot);
    }

    [Fact]
    public void From_library_emits_a_semi_join_on_the_caller_user_id()
    {
        var user = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var snapshot = Compile("tracks from library order by year desc", user);
        Assert.Equal(
            """
            SELECT r.mbid AS id, r.name AS title, a.name AS artist, rg.name AS album, r.first_release_year AS year, r.length_ms AS length_ms FROM catalog.recording r JOIN catalog.artist a ON a.id = r.artist_id LEFT JOIN catalog.release_group rg ON rg.id = r.release_group_id WHERE EXISTS (SELECT 1 FROM app.user_library ul WHERE ul.recording_id = r.id AND ul.user_id = @caller_user_id) ORDER BY r.first_release_year DESC LIMIT @row_limit
            --
            @caller_user_id = uuid 11111111-1111-1111-1111-111111111111
            @row_limit = int 100
            """,
            snapshot);
    }

    [Fact]
    public void Canonical_grunge_query()
    {
        var snapshot = Compile(
            "tracks where genre = \"grunge\" and year between 1990 and 2004 and artist != \"Nirvana\"");
        Assert.Equal(
            """
            SELECT r.mbid AS id, r.name AS title, a.name AS artist, rg.name AS album, r.first_release_year AS year, r.length_ms AS length_ms FROM catalog.recording r JOIN catalog.artist a ON a.id = r.artist_id LEFT JOIN catalog.release_group rg ON rg.id = r.release_group_id WHERE (((EXISTS (SELECT 1 FROM catalog.recording_genre lg JOIN catalog.genre g ON g.id = lg.genre_id WHERE lg.recording_id = r.id AND lower(g.name) = lower(@p0)) OR EXISTS (SELECT 1 FROM catalog.release_group_genre lg JOIN catalog.genre g ON g.id = lg.genre_id WHERE lg.release_group_id = r.release_group_id AND lower(g.name) = lower(@p0)) OR EXISTS (SELECT 1 FROM catalog.artist_genre lg JOIN catalog.genre g ON g.id = lg.genre_id WHERE lg.artist_id = r.artist_id AND lower(g.name) = lower(@p0))) AND r.first_release_year BETWEEN @p1 AND @p2) AND lower(a.name) <> lower(@p3)) LIMIT @row_limit
            --
            @p0 = text "grunge"
            @p1 = bigint 1990
            @p2 = bigint 2004
            @p3 = text "Nirvana"
            @row_limit = int 100
            """,
            snapshot);
    }

    [Fact]
    public void Limit_is_capped_at_the_row_cap()
    {
        var result = Engine.Compile("tracks limit 100000", CompileContext.Default);
        Assert.True(result.Success);
        Assert.Equal(MqlLimits.MaxRowCap, result.Query!.RowLimit);
    }

    private static string Compile(string input, Guid? userId = null)
    {
        var result = Engine.Compile(input, new CompileContext { CallerUserId = userId });
        Assert.True(result.Success, result.Errors.Count > 0 ? result.Errors[0].Message : "expected success");
        return Snapshot.Of(result.Query!);
    }
}
