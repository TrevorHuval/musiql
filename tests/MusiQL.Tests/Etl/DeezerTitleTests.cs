using MusiQL.Etl.Popularity;

namespace MusiQL.Tests.Etl;

public class DeezerTitleTests
{
    [Theory]
    [InlineData("Come As You Are (Remastered 2021)", "come as you are")]
    [InlineData("Bohemian Rhapsody - Remastered 2011", "bohemian rhapsody")]
    [InlineData("God’s Plan", "god's plan")]
    [InlineData("Lose Yourself [From \"8 Mile\"]", "lose yourself")]
    public void Title_core_drops_version_markers(string title, string expected) =>
        Assert.Equal(expected, DeezerFetcher.TitleCore(title));
}
