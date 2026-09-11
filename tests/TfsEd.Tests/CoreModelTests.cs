using TfsEd.Core;
using TfsEd.Core.Tfvc;
using TfsEd.Core.Workspaces;

namespace TfsEd.Tests;

public sealed class CoreModelTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("tfsed-core-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Theory]
    [InlineData("$/P/", "$/P")]
    [InlineData(@"$\P\Sub", "$/P/Sub")]
    [InlineData("$//P//a.txt", "$/P/a.txt")]
    [InlineData("$", "$/")]
    public void ServerPath_normalizes(string input, string expected) => Assert.Equal(expected, ServerPath.Normalize(input));

    [Fact]
    public void ServerPath_rejects_local_paths() => Assert.Throws<TfsEdException>(() => ServerPath.Normalize("C:/x"));

    [Fact]
    public void ServerPath_relations()
    {
        Assert.True(ServerPath.IsSameOrUnder("$/P/a", "$/p"));
        Assert.False(ServerPath.IsSameOrUnder("$/Pa", "$/P"));
        Assert.True(ServerPath.IsSameOrUnder("$/P", ServerPath.Root));
        Assert.Equal("Sub/a.txt", ServerPath.GetRelative("$/P", "$/P/Sub/a.txt"));
        Assert.Equal("$/P/x", ServerPath.Combine("$/P", "x"));
        Assert.Equal("$/x", ServerPath.Combine(ServerPath.Root, "x"));
        Assert.Equal("a.txt", ServerPath.GetFileName("$/P/a.txt"));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("T", null)]
    [InlineData("C123", 123)]
    [InlineData("c7", 7)]
    [InlineData("42", 42)]
    public void VersionSpec_parses(string? input, int? expected) => Assert.Equal(expected, VersionSpec.Parse(input).Changeset);

    [Theory]
    [InlineData("C0")]
    [InlineData("L:label")]
    [InlineData("D2024-01-01")]
    public void VersionSpec_rejects_unsupported(string input) => Assert.Throws<TfsEdException>(() => VersionSpec.Parse(input));

    [Fact]
    public void Baseline_round_trips()
    {
        var baseline = new Baseline();
        baseline.Set(new BaselineEntry("$/P", true, 3, 0, null, 0));
        baseline.Set(new BaselineEntry("$/P/Größe mit Leerzeichen.txt", false, 5, 12, "abc==", 638000000000000000));
        var file = Path.Combine(_dir, "baseline.tsv");

        baseline.Save(file);
        var loaded = Baseline.Load(file);

        Assert.Equal(2, loaded.Count);
        Assert.Equal(baseline.Get("$/P/Größe mit Leerzeichen.txt"), loaded.Get("$/p/größe mit leerzeichen.txt"));
        Assert.Equal(baseline.Get("$/P"), loaded.Get("$/P"));
    }

    [Fact]
    public void Workspace_maps_paths_and_is_found_from_subdirectories()
    {
        var workspace = Workspace.Create(Path.Combine(_dir, "ws"), CollectionUrl.Parse("https://tfs.example.com/Coll"), "$/P/Main");
        var sub = Directory.CreateDirectory(Path.Combine(workspace.Root, "a", "b")).FullName;

        Assert.Equal(Path.Combine(workspace.Root, "a", "x.cs"), workspace.ToLocalPath("$/P/Main/a/x.cs"));
        Assert.Equal("$/P/Main/a/b", workspace.ToServerPath(sub));
        Assert.Equal("$/P/Main", workspace.ToServerPath(workspace.Root));
        Assert.Equal(workspace.Root, Workspace.Find(sub)?.Root);
        Assert.Null(Workspace.Find(_dir));
        Assert.Throws<TfsEdException>(() => workspace.ToServerPath(_dir));
        Assert.Throws<TfsEdException>(() => Workspace.Create(sub, CollectionUrl.Parse("https://tfs.example.com/Coll"), "$/P"));
    }

    [Theory]
    [InlineData("app.user", true)]
    [InlineData("src/app.user", true)]
    [InlineData("src/keep.user", false)]
    [InlineData("src/bin", true)]
    [InlineData("docs/manual.pdf", true)]
    [InlineData("other/docs/manual.pdf", false)]
    [InlineData("sub/local.tmp", true)]
    [InlineData("local.tmp", false)]
    public void IgnoreMatcher_evaluates_rules(string path, bool ignored)
    {
        var matcher = IgnoreMatcher.Empty
            .WithRules(["# comment", "*.user", "!keep.user", "bin", @"\docs\*.pdf"], string.Empty)
            .WithRules(["*.tmp"], "sub");

        Assert.Equal(ignored, matcher.IsIgnored(path));
    }
}
