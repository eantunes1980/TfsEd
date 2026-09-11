using TfsEd.Core;
using TfsEd.Core.Tfvc;
using TfsEd.Core.Workspaces;

namespace TfsEd.Tests;

public abstract class WorkspaceTestBase : IDisposable
{
    protected WorkspaceTestBase()
    {
        Directory = System.IO.Directory.CreateTempSubdirectory("tfsed-ws-").FullName;
        Server.Commit(1, ("$/P", null), ("$/P/a.txt", "alpha\n"), ("$/P/src", null), ("$/P/src/b.cs", "class B {}\n"));
    }

    protected string Directory { get; }

    internal FakeTfvcServer Server { get; } = new();

    public void Dispose()
    {
        System.IO.Directory.Delete(Directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    protected Workspace CreateWorkspace() =>
        Workspace.Create(Path.Combine(Directory, "ws"), CollectionUrl.Parse("https://tfs.example.com/Coll"), "$/P");

    protected Task<GetResult> GetAsync(Workspace workspace, string? version = null, bool force = false, bool preview = false, string scope = "$/P") =>
        new GetOperation(workspace, Server).RunAsync(new GetOptions(scope, VersionSpec.Parse(version), force, preview), CancellationToken.None);

    protected static string Local(Workspace workspace, string relative) =>
        Path.Combine(workspace.Root, relative.Replace('/', Path.DirectorySeparatorChar));
}
