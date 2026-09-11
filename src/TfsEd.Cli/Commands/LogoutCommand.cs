using System.CommandLine;

namespace TfsEd.Cli.Commands;

internal static class LogoutCommand
{
    public static Command Create(CliServices services)
    {
        var command = new Command("logout", "Remove the stored personal access token of a collection.");
        command.SetAction((parseResult, _) => CommandRunner.RunAsync(services, async () =>
        {
            var collection = ServerConnection.ResolveCollection(parseResult, services);
            var removed = services.Credentials.Remove(collection);

            var config = services.Config.Load();
            if (string.Equals(config.DefaultCollection, collection.Value, StringComparison.OrdinalIgnoreCase))
            {
                config.DefaultCollection = null;
                services.Config.Save(config);
            }

            await services.Output.WriteLineAsync(removed
                ? $"Removed credentials for {collection}."
                : $"No stored credentials for {collection}.");
            return 0;
        }));
        return command;
    }
}
