using System.CommandLine;
using TfsEd.Core.Tfvc;

namespace TfsEd.Cli.Commands;

internal static class DirCommand
{
    public static Command Create(CliServices services)
    {
        var path = GlobalOptions.OptionalPath("Server path ($/...) or local path in a workspace (default: current directory).");
        var version = GlobalOptions.Version();
        var recursive = new Option<bool>("--recursive", "-r") { Description = "List all items below the path." };

        var command = new Command("dir", "List the items of a server folder.") { path, version, recursive };
        command.SetAction((parseResult, cancellationToken) => CommandRunner.RunAsync(services, async () =>
        {
            var (workspace, serverPath) = WorkspaceResolver.ResolveTarget(services, parseResult.GetValue(path));
            using var connection = ServerConnection.Open(parseResult, services, workspace?.Collection);
            var recursion = parseResult.GetValue(recursive) ? RecursionLevel.Full : RecursionLevel.OneLevel;
            var items = await connection.Client.GetItemsAsync(serverPath, VersionSpec.Parse(parseResult.GetValue(version)), recursion, cancellationToken);

            await services.Output.WriteLineAsync($"{serverPath}:");
            var children = items.Where(item => !ServerPath.AreEqual(item.Path, serverPath))
                .OrderBy(item => item.Path, ServerPath.Comparer)
                .ToList();
            foreach (var item in children)
            {
                var name = ServerPath.GetRelative(serverPath, item.Path) + (item.IsFolder ? "/" : string.Empty);
                var size = item.IsFolder ? string.Empty : item.Size.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
                await services.Output.WriteLineAsync($"  C{item.Version,-7} {size,14}  {name}");
            }

            await services.Output.WriteLineAsync($"{children.Count} item(s)");
            return 0;
        }));
        return command;
    }
}
