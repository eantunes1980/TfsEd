using System.CommandLine;
using TfsEd.Core;
using TfsEd.Core.Tfvc;
using TfsEd.Core.Workspaces;

namespace TfsEd.Cli.Commands;

internal static class WorkspaceCommand
{
    public static Command Create(CliServices services)
    {
        var command = new Command("workspace", "Create, list and delete local workspaces.");
        command.Subcommands.Add(CreateCreateCommand(services));
        command.Subcommands.Add(CreateListCommand(services));
        command.Subcommands.Add(CreateDeleteCommand(services));
        return command;
    }

    private static Command CreateCreateCommand(CliServices services)
    {
        var serverPath = new Argument<string>("server-path") { Description = "Server folder to map, e.g. '$/Project/Main'." };
        var localDir = new Argument<string?>("local-dir")
        {
            Description = "Local directory (default: last segment of the server path in the current directory).",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var command = new Command("create", "Map a server folder to a local directory.") { serverPath, localDir };
        command.SetAction((parseResult, cancellationToken) => CommandRunner.RunAsync(services, async () =>
        {
            var path = ServerPath.Normalize(parseResult.GetValue(serverPath)!);
            var directory = parseResult.GetValue(localDir) ?? ServerPath.GetFileName(path);
            if (directory.Length == 0)
            {
                throw new TfsEdException("Specify a local directory when mapping $/.");
            }

            var root = Path.GetFullPath(directory, services.WorkingDirectory);
            using var connection = ServerConnection.Open(parseResult, services);
            var items = await connection.Client.GetItemsAsync(path, VersionSpec.Latest, RecursionLevel.None, cancellationToken);
            var folder = items.FirstOrDefault(item => ServerPath.AreEqual(item.Path, path));
            if (folder is not { IsFolder: true })
            {
                throw new TfsEdException($"'{path}' is not a folder on the server.");
            }

            var workspace = Workspace.Create(root, connection.Client.Collection, folder.Path);
            var config = services.Config.Load();
            if (!config.Workspaces.Contains(workspace.Root, StringComparer.OrdinalIgnoreCase))
            {
                config.Workspaces.Add(workspace.Root);
                services.Config.Save(config);
            }

            await services.Output.WriteLineAsync($"Created workspace {workspace.Root} -> {workspace.ServerRoot}.");
            await services.Output.WriteLineAsync("Run 'tfsed get' inside it to download the files.");
            return 0;
        }));
        return command;
    }

    private static Command CreateListCommand(CliServices services)
    {
        var command = new Command("list", "List the workspaces created on this machine.");
        command.SetAction((_, _) => CommandRunner.RunAsync(services, async () =>
        {
            var roots = services.Config.Load().Workspaces;
            if (roots.Count == 0)
            {
                await services.Output.WriteLineAsync("No workspaces.");
            }

            foreach (var root in roots)
            {
                var line = File.Exists(Path.Combine(root, Workspace.MetadataDirectoryName, "workspace.json"))
                    ? Describe(Workspace.Load(root))
                    : $"{root}  (missing)";
                await services.Output.WriteLineAsync(line);
            }

            return 0;
        }));
        return command;
    }

    private static Command CreateDeleteCommand(CliServices services)
    {
        var localDir = new Argument<string?>("local-dir")
        {
            Description = "Workspace directory (default: the workspace containing the current directory).",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var command = new Command("delete", "Remove the workspace metadata. Local files are kept.") { localDir };
        command.SetAction((parseResult, _) => CommandRunner.RunAsync(services, async () =>
        {
            var workspace = WorkspaceResolver.Require(services, parseResult.GetValue(localDir));
            workspace.DeleteMetadata();

            var config = services.Config.Load();
            if (config.Workspaces.RemoveAll(root => string.Equals(root, workspace.Root, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                services.Config.Save(config);
            }

            await services.Output.WriteLineAsync($"Deleted workspace {workspace.Root} (files kept).");
            return 0;
        }));
        return command;
    }

    private static string Describe(Workspace workspace) =>
        $"{workspace.Root}  {workspace.ServerRoot}  {(workspace.Info.Version > 0 ? $"C{workspace.Info.Version}" : "(not downloaded)")}  {workspace.Info.Collection}";
}
