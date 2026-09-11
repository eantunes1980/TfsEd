using TfsEd.Core;

namespace TfsEd.Tests;

public class CollectionUrlTests
{
    [Theory]
    [InlineData("https://tfs.example.com/DefaultCollection", "https://tfs.example.com/DefaultCollection")]
    [InlineData("https://tfs.example.com/DefaultCollection/", "https://tfs.example.com/DefaultCollection")]
    [InlineData("  HTTPS://TFS.example.com/tfs/Coll  ", "https://tfs.example.com/tfs/Coll")]
    public void Parse_normalizes(string input, string expected) =>
        Assert.Equal(expected, CollectionUrl.Parse(input).Value);

    [Theory]
    [InlineData("")]
    [InlineData("DefaultCollection")]
    [InlineData("ftp://tfs.example.com/DefaultCollection")]
    [InlineData("https://tfs.example.com/DefaultCollection?x=1")]
    public void Parse_rejects_invalid(string input) =>
        Assert.Throws<TfsEdException>(() => CollectionUrl.Parse(input));

    [Fact]
    public void Combine_appends_relative_path() =>
        Assert.Equal(
            "https://tfs.example.com/DefaultCollection/_apis/projects?api-version=7.0",
            CollectionUrl.Parse("https://tfs.example.com/DefaultCollection/").Combine("/_apis/projects?api-version=7.0").AbsoluteUri);

    [Fact]
    public void Equality_ignores_case() =>
        Assert.Equal(CollectionUrl.Parse("https://TFS.example.com/Coll"), CollectionUrl.Parse("https://tfs.example.com/coll"));
}
