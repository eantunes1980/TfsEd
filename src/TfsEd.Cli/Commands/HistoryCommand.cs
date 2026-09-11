using System.CommandLine;

namespace TfsEd.Cli.Commands;

internal static class HistoryCommand
{
    public static Command Create(CliServices services)
    {
        var path = GlobalOptions.OptionalPath("Server path ($/...) or local path in a workspace (default: current directory).");
        var top = new Option<int>("--top", "-n") { Description = "Maximum number of changesets.", DefaultValueFactory = _ => 20 };

        var command = new Command("history", "Show the changesets that affected a path.") { path, top };
        command.SetAction((parseResult, cancellationToken) => CommandRunner.RunAsync(services, async () =>
        {
            var (workspace, serverPath) = WorkspaceResolver.ResolveTarget(services, parseResult.GetValue(path));
            using var connection = ServerConnection.Open(parseResult, services, workspace?.Collection);
            var history = await connection.Client.GetHistoryAsync(serverPath, parseResult.GetValue(top), cancellationToken);

            foreach (var changeset in history)
            {
                var comment = (changeset.Comment ?? string.Empty).ReplaceLineEndings(" ");
                await services.Output.WriteLineAsync(
                    $"C{changeset.Id,-7} {changeset.Date.ToLocalTime():yyyy-MM-dd HH:mm}  {changeset.Author,-24}  {comment}");
            }

            if (history.Count == 0)
            {
                await services.Output.WriteLineAsync($"No history for {serverPath}.");
            }

            return 0;
        }));
        return command;
    }
}
