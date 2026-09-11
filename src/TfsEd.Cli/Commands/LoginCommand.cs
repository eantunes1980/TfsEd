using System.CommandLine;
using TfsEd.Core;
using TfsEd.Server;

namespace TfsEd.Cli.Commands;

internal static class LoginCommand
{
    public static Command Create(CliServices services)
    {
        var url = new Argument<string>("collection-url")
        {
            Description = "Collection URL, e.g. https://tfs.example.com/DefaultCollection",
        };
        var pat = new Option<string?>("--pat")
        {
            Description = "Personal access token. Prompted if omitted; '-' reads it from stdin.",
        };
        var caCert = new Option<FileInfo?>("--ca-cert")
        {
            Description = "Root CA certificate (PEM or DER) to trust in addition to the system store.",
        };

        var command = new Command("login", "Verify a personal access token, store it securely and make the collection the default.")
        {
            url, pat, caCert,
        };
        command.SetAction((parseResult, cancellationToken) => CommandRunner.RunAsync(services, async () =>
        {
            var collection = CollectionUrl.Parse(parseResult.GetValue(url)!);
            var config = services.Config.Load();
            var caPath = parseResult.GetValue(caCert)?.FullName ?? config.CaCertificatePath;
            if (caPath is not null && !File.Exists(caPath))
            {
                throw new TfsEdException($"CA certificate not found: {caPath}");
            }

            var token = parseResult.GetValue(pat);
            if (token is null or "-")
            {
                token = SecretPrompt.Read("Personal access token: ", services);
            }

            token = token.Trim();
            if (token.Length == 0)
            {
                throw new TfsEdException("No personal access token given.");
            }

            using var http = services.CreateHttpClient(caPath);
            var identity = await new AdoClient(http, collection, token).GetConnectionDataAsync(cancellationToken);

            services.Credentials.Set(collection, token);
            config.DefaultCollection = collection.Value;
            config.CaCertificatePath = caPath;
            services.Config.Save(config);

            await services.Output.WriteLineAsync(
                $"Logged in to {collection} as {identity.AuthenticatedUser?.ProviderDisplayName ?? "<unknown>"}. "
                + $"Token stored in {services.Credentials.Description}.");
            return 0;
        }));
        return command;
    }
}
