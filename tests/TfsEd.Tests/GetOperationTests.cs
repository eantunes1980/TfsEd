using TfsEd.Core.Workspaces;

namespace TfsEd.Tests;

public sealed class GetOperationTests : WorkspaceTestBase
{
    [Fact]
    public async Task Initial_get_downloads_the_tree()
    {
        var workspace = CreateWorkspace();

        var result = await GetAsync(workspace);

        Assert.Equal(2, result.Count(GetActionKind.Get));
        Assert.Equal("alpha\n", File.ReadAllText(Local(workspace, "a.txt")));
        Assert.Equal("class B {}\n", File.ReadAllText(Local(workspace, "src/b.cs")));
        var reloaded = Workspace.Load(workspace.Root);
        Assert.Equal(1, reloaded.Info.Version);
        Assert.Equal(4, reloaded.Baseline.Count);
    }

    [Fact]
    public async Task Second_get_applies_server_changes()
    {
        var workspace = CreateWorkspace();
        await GetAsync(workspace);
        Server.Commit(2, ("$/P", null), ("$/P/a.txt", "alpha 2\n"), ("$/P/c.txt", "new\n"));

        var result = await GetAsync(workspace);

        Assert.Equal("alpha 2\n", File.ReadAllText(Local(workspace, "a.txt")));
        Assert.Equal("new\n", File.ReadAllText(Local(workspace, "c.txt")));
        Assert.False(File.Exists(Local(workspace, "src/b.cs")));
        Assert.False(System.IO.Directory.Exists(Local(workspace, "src")));
        Assert.Equal(1, result.Count(GetActionKind.Replace));
        Assert.Equal(1, result.Count(GetActionKind.Get));
        Assert.Equal(1, result.Count(GetActionKind.Delete));
        Assert.Equal(2, workspace.Info.Version);
    }

    [Fact]
    public async Task Up_to_date_workspace_downloads_nothing()
    {
        var workspace = CreateWorkspace();
        await GetAsync(workspace);
        var downloads = Server.DownloadedFiles;

        var result = await GetAsync(workspace);

        Assert.Empty(result.Actions);
        Assert.Equal(downloads, Server.DownloadedFiles);
    }

    [Fact]
    public async Task Local_modification_conflicts_unless_forced()
    {
        var workspace = CreateWorkspace();
        await GetAsync(workspace);
        File.WriteAllText(Local(workspace, "a.txt"), "my local edit\n");
        Server.Commit(2, ("$/P", null), ("$/P/a.txt", "server edit\n"), ("$/P/src", null), ("$/P/src/b.cs", "class B {}\n"));

        var result = await GetAsync(workspace);

        Assert.Equal(GetActionKind.Conflict, Assert.Single(result.Actions).Kind);
        Assert.Equal("my local edit\n", File.ReadAllText(Local(workspace, "a.txt")));

        await GetAsync(workspace, force: true);

        Assert.Equal("server edit\n", File.ReadAllText(Local(workspace, "a.txt")));
    }

    [Fact]
    public async Task Server_deletion_of_locally_modified_file_conflicts()
    {
        var workspace = CreateWorkspace();
        await GetAsync(workspace);
        File.WriteAllText(Local(workspace, "src/b.cs"), "class B { int changed; }\n");
        Server.Commit(2, ("$/P", null), ("$/P/a.txt", "alpha\n"));

        var result = await GetAsync(workspace);

        Assert.Equal(GetActionKind.Conflict, Assert.Single(result.Actions).Kind);
        Assert.True(File.Exists(Local(workspace, "src/b.cs")));
    }

    [Fact]
    public async Task Existing_identical_file_is_adopted_and_different_file_conflicts()
    {
        var workspace = CreateWorkspace();
        File.WriteAllText(Local(workspace, "a.txt"), "alpha\n");
        System.IO.Directory.CreateDirectory(Local(workspace, "src"));
        File.WriteAllText(Local(workspace, "src/b.cs"), "something else\n");

        var result = await GetAsync(workspace);

        Assert.Equal(GetActionKind.Conflict, Assert.Single(result.Actions).Kind);
        Assert.Equal(0, Server.DownloadedFiles);
        Assert.NotNull(workspace.Baseline.Get("$/P/a.txt"));
        Assert.Null(workspace.Baseline.Get("$/P/src/b.cs"));
    }

    [Fact]
    public async Task Missing_file_is_restored_only_with_force()
    {
        var workspace = CreateWorkspace();
        await GetAsync(workspace);
        File.Delete(Local(workspace, "a.txt"));

        Assert.Empty((await GetAsync(workspace)).Actions);
        Assert.False(File.Exists(Local(workspace, "a.txt")));

        var forced = await GetAsync(workspace, force: true);

        Assert.Equal(GetActionKind.Get, Assert.Single(forced.Actions).Kind);
        Assert.True(File.Exists(Local(workspace, "a.txt")));
    }

    [Fact]
    public async Task Preview_changes_nothing()
    {
        var workspace = CreateWorkspace();

        var result = await GetAsync(workspace, preview: true);

        Assert.Equal(2, result.Count(GetActionKind.Get));
        Assert.False(File.Exists(Local(workspace, "a.txt")));
        Assert.Equal(0, workspace.Baseline.Count);
    }

    [Fact]
    public async Task Get_specific_version_goes_back_in_time()
    {
        var workspace = CreateWorkspace();
        Server.Commit(2, ("$/P", null), ("$/P/a.txt", "alpha 2\n"), ("$/P/src", null), ("$/P/src/b.cs", "class B {}\n"));
        await GetAsync(workspace);

        await GetAsync(workspace, version: "C1");

        Assert.Equal("alpha\n", File.ReadAllText(Local(workspace, "a.txt")));
        Assert.Equal(1, workspace.Info.Version);
    }

    [Fact]
    public async Task Scoped_get_only_touches_the_scope()
    {
        var workspace = CreateWorkspace();
        await GetAsync(workspace);
        Server.Commit(2, ("$/P", null), ("$/P/a.txt", "alpha 2\n"), ("$/P/src", null), ("$/P/src/b.cs", "class B2 {}\n"));

        await GetAsync(workspace, scope: "$/P/src");

        Assert.Equal("class B2 {}\n", File.ReadAllText(Local(workspace, "src/b.cs")));
        Assert.Equal("alpha\n", File.ReadAllText(Local(workspace, "a.txt")));
        Assert.Equal(1, workspace.Info.Version);
    }
}
