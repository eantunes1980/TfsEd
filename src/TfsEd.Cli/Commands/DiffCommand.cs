using System.CommandLine;
using TfsEd.Core.Diff;
using TfsEd.Core.Workspaces;

namespace TfsEd.Cli.Commands;

internal static class DiffCommand
{
    public static Command Create(CliServices services)
    {
        var path = GlobalOptions.OptionalPath("Local or server path inside the workspace (default: current directory).");
        var command = new Command("diff", "Show local modifications as a unified diff against the workspace version.") { path };
        command.SetAction((parseResult, cancellationToken) => CommandRunner.RunAsync(services, async () =>
        {
            var workspace = WorkspaceResolver.Require(services, parseResult.GetValue(path));
            var scope = WorkspaceResolver.ToServerPath(workspace, parseResult.GetValue(path), services);
            var changes = StatusScanner.Scan(workspace, scope)
                .Where(change => change.Kind is ChangeKind.Modified or ChangeKind.Missing && !change.IsFolder)
                .ToList();
            if (changes.Count == 0)
            {
                await services.Output.WriteLineAsync("No local modifications.");
                return 0;
            }

            using var connection = ServerConnection.Open(parseResult, services, workspace.Collection);
            foreach (var change in changes)
            {
                var entry = workspace.Baseline.Get(change.ServerPath)!;
                var original = await connection.Client.GetContentAsync(change.ServerPath, entry.Version, cancellationToken);
                var local = change.Kind == ChangeKind.Missing ? [] : await File.ReadAllBytesAsync(change.LocalPath, cancellationToken);
                var localLabel = change.Kind == ChangeKind.Missing ? "/dev/null (deleted locally)" : WorkspaceResolver.Display(services, change.LocalPath);
                await services.Output.WriteAsync(UnifiedDiff.Format($"{change.ServerPath};C{entry.Version}", localLabel, original, local));
            }

            return 0;
        }));
        return command;
    }
}
