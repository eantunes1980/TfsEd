using TfsEd.Core;
using TfsEd.Core.Tfvc;
using TfsEd.Core.Workspaces;

namespace TfsEd.Cli;

/// <summary>Maps command-line path arguments (local or <c>$/</c> server paths) to workspaces and server paths.</summary>
internal static class WorkspaceResolver
{
    /// <summary>Finds the workspace containing the local path argument, or the working directory.</summary>
    public static Workspace Require(CliServices services, string? pathArgument = null)
    {
        var start = pathArgument is null || ServerPath.IsServerPath(pathArgument)
            ? services.WorkingDirectory
            : Path.GetFullPath(pathArgument, services.WorkingDirectory);
        return Workspace.Find(start)
            ?? throw new TfsEdException("Not inside a TfsEd workspace. Create one with 'tfsed workspace create <server-path> [local-dir]'.");
    }

    /// <summary>Resolves an optional path argument to a server path mapped in <paramref name="workspace"/>.</summary>
    public static string ToServerPath(Workspace workspace, string? pathArgument, CliServices services)
    {
        if (pathArgument is null)
        {
            return workspace.ToServerPath(services.WorkingDirectory);
        }

        if (!ServerPath.IsServerPath(pathArgument))
        {
            return workspace.ToServerPath(Path.GetFullPath(pathArgument, services.WorkingDirectory));
        }

        var serverPath = ServerPath.Normalize(pathArgument);
        return ServerPath.IsSameOrUnder(serverPath, workspace.ServerRoot)
            ? serverPath
            : throw new TfsEdException($"'{serverPath}' is not mapped in this workspace ({workspace.ServerRoot}).");
    }

    /// <summary>
    /// For commands that also work without a workspace: a server path is used as is,
    /// a local path (or none) must be inside a workspace.
    /// </summary>
    public static (Workspace? Workspace, string ServerPath) ResolveTarget(CliServices services, string? pathArgument)
    {
        if (pathArgument is not null && ServerPath.IsServerPath(pathArgument))
        {
            return (Workspace.Find(services.WorkingDirectory), ServerPath.Normalize(pathArgument));
        }

        var workspace = Require(services, pathArgument);
        return (workspace, ToServerPath(workspace, pathArgument, services));
    }

    public static string Display(CliServices services, string localPath)
    {
        var relative = Path.GetRelativePath(services.WorkingDirectory, localPath);
        return relative == "." ? "." : relative;
    }
}
