using System.CommandLine;
using TfsEd.Core.Workspaces;

namespace TfsEd.Cli.Commands;

internal static class StatusCommand
{
    public static Command Create(CliServices services)
    {
        var path = GlobalOptions.OptionalPath("Local or server path inside the workspace (default: current directory).");
        var command = new Command("status", "Show locally modified, missing and untracked files (works offline).") { path };
        command.SetAction((parseResult, _) => CommandRunner.RunAsync(services, async () =>
        {
            var workspace = WorkspaceResolver.Require(services, parseResult.GetValue(path));
            var scope = WorkspaceResolver.ToServerPath(workspace, parseResult.GetValue(path), services);
            var changes = StatusScanner.Scan(workspace, scope);

            var output = services.Output;
            var version = workspace.Info.Version > 0 ? $"C{workspace.Info.Version}" : "not downloaded yet";
            await output.WriteLineAsync($"Workspace {workspace.ServerRoot} ({version})");
            if (changes.Count == 0)
            {
                await output.WriteLineAsync("No local changes.");
                return 0;
            }

            foreach (var change in changes)
            {
                var marker = change.Kind switch
                {
                    ChangeKind.Modified => 'M',
                    ChangeKind.Missing => '!',
                    _ => '?',
                };
                var suffix = change.IsFolder ? "/" : string.Empty;
                await output.WriteLineAsync($"{marker}  {WorkspaceResolver.Display(services, change.LocalPath)}{suffix}");
            }

            await output.WriteLineAsync("(M = modified, ! = missing, ? = not under version control)");
            return 0;
        }));
        return command;
    }
}
