using TfsEd.Core.Workspaces;

namespace TfsEd.Tests;

public sealed class StatusScannerTests : WorkspaceTestBase
{
    [Fact]
    public async Task Clean_workspace_has_no_changes()
    {
        var workspace = CreateWorkspace();
        await GetAsync(workspace);

        Assert.Empty(StatusScanner.Scan(workspace, "$/P"));
    }

    [Fact]
    public async Task Reports_modified_missing_and_untracked_items()
    {
        var workspace = CreateWorkspace();
        await GetAsync(workspace);
        File.WriteAllText(Local(workspace, "a.txt"), "changed content\n");
        File.Delete(Local(workspace, "src/b.cs"));
        File.WriteAllText(Local(workspace, "new.txt"), "x");
        System.IO.Directory.CreateDirectory(Local(workspace, "newdir"));

        var changes = StatusScanner.Scan(workspace, "$/P");

        Assert.Collection(changes,
            c => Assert.Equal((ChangeKind.Modified, "$/P/a.txt"), (c.Kind, c.ServerPath)),
            c => Assert.Equal((ChangeKind.Untracked, "$/P/new.txt"), (c.Kind, c.ServerPath)),
            c => Assert.Equal((ChangeKind.Untracked, "$/P/newdir", true), (c.Kind, c.ServerPath, c.IsFolder)),
            c => Assert.Equal((ChangeKind.Missing, "$/P/src/b.cs"), (c.Kind, c.ServerPath)));
    }

    [Fact]
    public async Task Touched_but_identical_file_is_unchanged()
    {
        var workspace = CreateWorkspace();
        await GetAsync(workspace);
        File.SetLastWriteTimeUtc(Local(workspace, "a.txt"), DateTime.UtcNow.AddHours(1));

        Assert.Empty(StatusScanner.Scan(workspace, "$/P"));
    }

    [Fact]
    public async Task Tfignore_hides_untracked_items_but_not_tracked_ones()
    {
        var workspace = CreateWorkspace();
        await GetAsync(workspace);
        File.WriteAllText(Local(workspace, ".tfignore"), "*.user\nbin\n*.cs\n");
        File.WriteAllText(Local(workspace, "app.user"), "x");
        System.IO.Directory.CreateDirectory(Local(workspace, "src/bin"));
        File.WriteAllText(Local(workspace, "src/bin/out.dll"), "x");
        File.WriteAllText(Local(workspace, "src/b.cs"), "tracked and modified\n");

        var changes = StatusScanner.Scan(workspace, "$/P");

        Assert.Collection(changes,
            c => Assert.Equal((ChangeKind.Untracked, "$/P/.tfignore"), (c.Kind, c.ServerPath)),
            c => Assert.Equal((ChangeKind.Modified, "$/P/src/b.cs"), (c.Kind, c.ServerPath)));
    }

    [Fact]
    public async Task Scope_limits_the_scan()
    {
        var workspace = CreateWorkspace();
        await GetAsync(workspace);
        File.WriteAllText(Local(workspace, "a.txt"), "changed content\n");

        Assert.Empty(StatusScanner.Scan(workspace, "$/P/src"));
    }
}
