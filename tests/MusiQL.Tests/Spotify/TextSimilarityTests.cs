using MusiQL.Api.Spotify;
using Xunit;

namespace MusiQL.Tests.Spotify;

public class TextSimilarityTests
{
    [Theory]
    [InlineData("Heart-Shaped Box", "heart shaped box")]
    [InlineData("Smells Like Teen Spirit (Remastered)", "smells like teen spirit")]
    [InlineData("Björk", "bjork")]
    [InlineData("Come as You Are - 2021 Remaster", "come as you are")]
    [InlineData("Song (feat. Someone Else)", "song")]
    public void Normalize_folds_noise(string input, string expected)
    {
        Assert.Equal(expected, TextSimilarity.Normalize(input));
    }

    [Fact]
    public void Ratio_is_one_for_identical_after_normalization()
    {
        Assert.Equal(1.0, TextSimilarity.Ratio("Lithium", "lithium!"), 3);
    }

    [Fact]
    public void Ratio_stays_high_across_remaster_annotation()
    {
        var ratio = TextSimilarity.Ratio("Black", "Black (Remastered 2016)");
        Assert.True(ratio > 0.9, $"expected high ratio, got {ratio}");
    }

    [Fact]
    public void Ratio_is_low_for_unrelated_titles()
    {
        Assert.True(TextSimilarity.Ratio("Lithium", "Jeremy") < 0.5);
    }
}
