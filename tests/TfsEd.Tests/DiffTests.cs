using System.Text;
using TfsEd.Core.Diff;

namespace TfsEd.Tests;

public class DiffTests
{
    private static string Diff(string oldText, string newText) =>
        UnifiedDiff.Format("old", "new", Encoding.UTF8.GetBytes(oldText), Encoding.UTF8.GetBytes(newText));

    [Fact]
    public void Equal_content_gives_empty_diff() => Assert.Equal(string.Empty, Diff("a\nb\n", "a\nb\n"));

    [Fact]
    public void Changed_line_gives_single_hunk() =>
        Assert.Equal("--- old\n+++ new\n@@ -1,3 +1,3 @@\n a\n-b\n+B\n c\n", Diff("a\nb\nc\n", "a\nB\nc\n"));

    [Fact]
    public void Insert_into_empty_file() =>
        Assert.Equal("--- old\n+++ new\n@@ -0,0 +1,1 @@\n+x\n", Diff(string.Empty, "x\n"));

    [Fact]
    public void Distant_changes_give_separate_hunks()
    {
        var lines = Enumerable.Range(1, 20).Select(i => $"line {i}").ToList();
        var changed = lines.ToList();
        changed[0] = "first";
        changed[19] = "last";

        var diff = Diff(string.Join('\n', lines) + "\n", string.Join('\n', changed) + "\n");

        Assert.Equal(2, diff.Split('\n').Count(line => line.StartsWith("@@", StringComparison.Ordinal)));
        Assert.Contains("@@ -17,4 +17,4 @@", diff);
    }

    [Fact]
    public void Line_ending_only_change_is_reported()
    {
        var diff = Diff("a\r\nb\r\n", "a\nb\n");

        Assert.Contains("line endings", diff);
    }

    [Fact]
    public void Binary_content_is_not_diffed() =>
        Assert.Equal("Binary files old and new differ\n", UnifiedDiff.Format("old", "new", [1, 0, 2], [1, 0, 3]));

    [Fact]
    public void Edit_script_reconstructs_both_sides()
    {
        var random = new Random(42);
        for (var run = 0; run < 200; run++)
        {
            var oldLines = Enumerable.Range(0, random.Next(0, 30)).Select(_ => ((char)('a' + random.Next(4))).ToString()).ToList();
            var newLines = Enumerable.Range(0, random.Next(0, 30)).Select(_ => ((char)('a' + random.Next(4))).ToString()).ToList();

            var edits = MyersDiff.Compute(oldLines, newLines);

            Assert.Equal(oldLines, edits.Where(e => e.Kind != EditKind.Insert).Select(e => oldLines[e.OldIndex]));
            Assert.Equal(newLines, edits.Where(e => e.Kind != EditKind.Delete).Select(e => newLines[e.NewIndex]));
        }
    }
}
