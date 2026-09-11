using System.CommandLine;
using TfsEd.Core.Tfvc;
using TfsEd.Core.Workspaces;

namespace TfsEd.Cli.Commands;

internal static class GetCommand
{
    public static Command Create(CliServices services)
    {
        var path = GlobalOptions.OptionalPath("Local or server path inside the workspace (default: current directory).");
        var version = GlobalOptions.Version();
        var force = new Option<bool>("--force", "-f") { Description = "Overwrite local modifications and restore missing files." };
        var preview = new Option<bool>("--preview") { Description = "Show what would happen without changing anything." };
        var quiet = new Option<bool>("--quiet", "-q") { Description = "Only print conflicts and the summary." };

        var command = new Command("get", "Download the latest (or a specific) version into the workspace.")
        {
            path, version, force, preview, quiet,
        };
        command.SetAction((parseResult, cancellationToken) => CommandRunner.RunAsync(services, async () =>
        {
            var workspace = WorkspaceResolver.Require(services, parseResult.GetValue(path));
            var scope = WorkspaceResolver.ToServerPath(workspace, parseResult.GetValue(path), services);
            var options = new GetOptions(scope, VersionSpec.Parse(parseResult.GetValue(version)), parseResult.GetValue(force), parseResult.GetValue(preview));

            using var connection = ServerConnection.Open(parseResult, services, workspace.Collection);
            var result = await new GetOperation(workspace, connection.Client).RunAsync(options, cancellationToken);

            var output = services.Output;
            foreach (var action in result.Actions.OrderBy(action => action.ServerPath, ServerPath.Comparer))
            {
                if (parseResult.GetValue(quiet) && action.Kind != GetActionKind.Conflict)
                {
                    continue;
                }

                var detail = action.Detail is null ? string.Empty : $" ({action.Detail})";
                await output.WriteLineAsync($"{action.Kind.ToString().ToLowerInvariant(),-9}{WorkspaceResolver.Display(services, action.LocalPath)}{detail}");
            }

            var conflicts = result.Count(GetActionKind.Conflict);
            var summary = result.Actions.Count == 0
                ? $"All files are up to date at C{result.Version}."
                : $"{result.Count(GetActionKind.Get)} new, {result.Count(GetActionKind.Replace)} replaced, "
                  + $"{result.Count(GetActionKind.Delete)} deleted, {conflicts} conflict(s) at C{result.Version}.";
            await output.WriteLineAsync(options.Preview ? $"Preview: {summary}" : summary);
            if (conflicts > 0)
            {
                await services.Error.WriteLineAsync("Conflicting files were left untouched. Use --force to overwrite them with the server version.");
            }

            return conflicts > 0 ? 1 : 0;
        }));
        return command;
    }
}
