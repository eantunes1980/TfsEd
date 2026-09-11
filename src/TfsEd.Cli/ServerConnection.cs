using System.CommandLine;
using TfsEd.Core;
using TfsEd.Server;

namespace TfsEd.Cli;

/// <summary>An authenticated client for the collection selected on the command line or in the config.</summary>
internal sealed class ServerConnection : IDisposable
{
    private const string PatVariable = "TFSED_PAT";
    private const string CollectionVariable = "TFSED_COLLECTION";

    private readonly HttpClient _http;

    private ServerConnection(HttpClient http, AdoClient client, string credentialSource)
    {
        _http = http;
        Client = client;
        CredentialSource = credentialSource;
    }

    public AdoClient Client { get; }

    public string CredentialSource { get; }

    public static CollectionUrl ResolveCollection(ParseResult parseResult, CliServices services)
    {
        var raw = parseResult.GetValue(GlobalOptions.Collection)
            ?? services.GetEnvironmentVariable(CollectionVariable)
            ?? services.Config.Load().DefaultCollection
            ?? throw new TfsEdException("No collection configured. Run 'tfsed login <collection-url>' first.");
        return CollectionUrl.Parse(raw);
    }

    public static ServerConnection Open(ParseResult parseResult, CliServices services)
    {
        var collection = ResolveCollection(parseResult, services);

        string credentialSource;
        var pat = services.GetEnvironmentVariable(PatVariable);
        if (!string.IsNullOrEmpty(pat))
        {
            credentialSource = $"{PatVariable} environment variable";
        }
        else
        {
            pat = services.Credentials.Get(collection)
                ?? throw new TfsEdException($"No credentials stored for {collection}. Run 'tfsed login {collection}'.");
            credentialSource = services.Credentials.Description;
        }

        var http = services.CreateHttpClient(services.Config.Load().CaCertificatePath);
        return new ServerConnection(http, new AdoClient(http, collection, pat), credentialSource);
    }

    public void Dispose() => _http.Dispose();
}
