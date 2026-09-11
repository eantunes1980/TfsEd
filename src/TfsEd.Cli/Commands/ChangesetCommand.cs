using System.CommandLine;
using TfsEd.Core.Workspaces;

namespace TfsEd.Cli.Commands;

internal static class ChangesetCommand
{
    public static Command Create(CliServices services)
    {
        var id = new Argument<int>("id") { Description = "Changeset number." };
        var command = new Command("changeset", "Show the details and changed items of a changeset.") { id };
        command.SetAction((parseResult, cancellationToken) => CommandRunner.RunAsync(services, async () =>
        {
            var workspace = Workspace.Find(services.WorkingDirectory);
            using var connection = ServerConnection.Open(parseResult, services, workspace?.Collection);
            var details = await connection.Client.GetChangesetAsync(parseResult.GetValue(id), cancellationToken);

            var output = services.Output;
            var summary = details.Summary;
            await output.WriteLineAsync($"Changeset: {summary.Id}");
            await output.WriteLineAsync($"User:      {summary.Author}{(summary.AuthorUniqueName is null ? string.Empty : $" ({summary.AuthorUniqueName})")}");
            await output.WriteLineAsync($"Date:      {summary.Date.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
            await output.WriteLineAsync("Comment:");
            foreach (var line in (summary.Comment ?? string.Empty).ReplaceLineEndings("\n").Split('\n'))
            {
                await output.WriteLineAsync($"  {line}");
            }

            await output.WriteLineAsync($"Items ({details.Changes.Count}):");
            foreach (var change in details.Changes)
            {
                await output.WriteLineAsync($"  {change.ChangeType,-22} {change.Path}");
            }

            return 0;
        }));
        return command;
    }
}
