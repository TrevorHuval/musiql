using System.Net.Http.Json;
using MusiQL.Api.Contracts;
using Xunit.Abstractions;

namespace MusiQL.Tests.Api;

[Collection("api")]
public class CatalogLookupTests(ApiFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public async Task Genre_autocomplete_matches_a_prefix()
    {
        if (Skip())
        {
            return;
        }

        var client = await fixture.RegisterClientAsync();
        var results = await client.GetFromJsonAsync<List<Suggestion>>("/api/catalog/genres?q=grun");
        Assert.Contains(results!, g => g.Name.Equals("grunge", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Artist_autocomplete_matches_a_prefix_case_insensitively()
    {
        if (Skip())
        {
            return;
        }

        var client = await fixture.RegisterClientAsync();
        var results = await client.GetFromJsonAsync<List<Suggestion>>("/api/catalog/artists?q=pearl");
        Assert.Contains(results!, a => a.Name == "Pearl Jam");
    }

    [Fact]
    public async Task Artist_autocomplete_is_empty_without_a_query()
    {
        if (Skip())
        {
            return;
        }

        var client = await fixture.RegisterClientAsync();
        var results = await client.GetFromJsonAsync<List<Suggestion>>("/api/catalog/artists");
        Assert.Empty(results!);
    }

    [Fact]
    public async Task Field_metadata_describes_the_schema()
    {
        if (Skip())
        {
            return;
        }

        var client = await fixture.RegisterClientAsync();
        var schema = await client.GetFromJsonAsync<SchemaResponse>("/api/catalog/fields");

        var tracks = schema!.Entities.SingleOrDefault(e => e.Name == "tracks");
        Assert.NotNull(tracks);
        Assert.True(tracks!.SupportsLibrary);

        var genre = tracks.Fields.Single(f => f.Name == "genre");
        Assert.Equal("genre", genre.Type);

        var year = tracks.Fields.Single(f => f.Name == "year");
        Assert.Equal("number", year.Type);
        Assert.Contains("between", year.Operators);

        var title = tracks.Fields.Single(f => f.Name == "title");
        Assert.Contains("track", title.Aliases);

        Assert.Contains(tracks.Columns, c => c.Name == "artist");
    }

    private bool Skip()
    {
        if (fixture.Available)
        {
            return false;
        }

        output.WriteLine("Postgres not reachable on localhost:5442; skipping API test.");
        return true;
    }
}
