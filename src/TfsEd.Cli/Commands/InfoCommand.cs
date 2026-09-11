using System.CommandLine;

namespace TfsEd.Cli.Commands;

internal static class InfoCommand
{
    public static Command Create(CliServices services)
    {
        var command = new Command("info", "Show the connected collection, the authenticated user and the available projects.");
        command.SetAction((parseResult, cancellationToken) => CommandRunner.RunAsync(services, async () =>
        {
            using var connection = ServerConnection.Open(parseResult, services);
            var client = connection.Client;
            var data = await client.GetConnectionDataAsync(cancellationToken);
            var projects = await client.GetProjectsAsync(cancellationToken);

            var output = services.Output;
            await output.WriteLineAsync($"Collection : {client.Collection}");
            await output.WriteLineAsync($"User       : {data.AuthenticatedUser?.ProviderDisplayName} ({data.AuthenticatedUser?.Id})");
            await output.WriteLineAsync($"Instance   : {data.InstanceId}");
            await output.WriteLineAsync($"Deployment : {data.DeploymentType}");
            await output.WriteLineAsync($"Credential : {connection.CredentialSource}");
            await output.WriteLineAsync($"Projects   : {projects.Count}");
            foreach (var project in projects.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            {
                await output.WriteLineAsync($"  {project.Name}");
            }

            return 0;
        }));
        return command;
    }
}
