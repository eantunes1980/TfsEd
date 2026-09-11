using System.Net;
using TfsEd.Cli;
using TfsEd.Core;
using TfsEd.Core.Configuration;
using TfsEd.Core.Credentials;

namespace TfsEd.Tests;

public sealed class CliTests : IDisposable
{
    private const string Url = "https://tfs.example.com/DefaultCollection";

    private readonly string _dir = Directory.CreateTempSubdirectory("tfsed-cli-").FullName;
    private readonly InMemoryCredentialStore _credentials = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private readonly Dictionary<string, string> _environment = [];
    private Func<HttpRequestMessage, HttpResponseMessage> _respond = request =>
        request.RequestUri!.AbsolutePath.EndsWith("/_apis/projects")
            ? StubHttpHandler.Json("""{"count":1,"value":[{"name":"Alpha"}]}""")
            : StubHttpHandler.Json("""{"authenticatedUser":{"id":"u1","providerDisplayName":"Jane Doe"},"deploymentType":"onPremises"}""");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private ConfigStore Config => new(Path.Combine(_dir, "config.json"));

    private Task<int> RunAsync(params string[] args)
    {
        var services = new CliServices
        {
            Config = Config,
            Credentials = _credentials,
            CreateHttpClient = _ => new HttpClient(new StubHttpHandler(r => _respond(r))),
            GetEnvironmentVariable = name => _environment.GetValueOrDefault(name),
            Input = new StringReader(string.Empty),
            Output = _output,
            Error = _error,
            IsInteractive = false,
        };
        return CliApp.Create(services).Parse(args).InvokeAsync();
    }

    [Fact]
    public async Task Login_stores_token_and_default_collection()
    {
        var exit = await RunAsync("login", Url, "--pat", "secret");

        Assert.Equal(0, exit);
        Assert.Equal("secret", _credentials.Get(CollectionUrl.Parse(Url)));
        Assert.Equal(Url, Config.Load().DefaultCollection);
        Assert.Contains("Jane Doe", _output.ToString());
    }

    [Fact]
    public async Task Login_with_rejected_token_stores_nothing()
    {
        _respond = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized);

        var exit = await RunAsync("login", Url, "--pat", "bad");

        Assert.Equal(1, exit);
        Assert.Null(_credentials.Get(CollectionUrl.Parse(Url)));
        Assert.Contains("Authentication failed", _error.ToString());
    }

    [Fact]
    public async Task Info_uses_stored_login()
    {
        await RunAsync("login", Url, "--pat", "secret");

        var exit = await RunAsync("info");

        Assert.Equal(0, exit);
        Assert.Contains("Alpha", _output.ToString());
        Assert.Contains("in-memory", _output.ToString());
    }

    [Fact]
    public async Task Info_prefers_pat_from_environment()
    {
        _environment["TFSED_COLLECTION"] = Url;
        _environment["TFSED_PAT"] = "env-secret";

        var exit = await RunAsync("info");

        Assert.Equal(0, exit);
        Assert.Contains("TFSED_PAT", _output.ToString());
    }

    [Fact]
    public async Task Info_without_login_fails_with_hint()
    {
        var exit = await RunAsync("info");

        Assert.Equal(1, exit);
        Assert.Contains("tfsed login", _error.ToString());
    }

    [Fact]
    public async Task Logout_removes_token_and_default()
    {
        await RunAsync("login", Url, "--pat", "secret");

        var exit = await RunAsync("logout");

        Assert.Equal(0, exit);
        Assert.Null(_credentials.Get(CollectionUrl.Parse(Url)));
        Assert.Null(Config.Load().DefaultCollection);
    }

    private sealed class InMemoryCredentialStore : ICredentialStore
    {
        private readonly Dictionary<CollectionUrl, string> _secrets = [];

        public string Description => "in-memory store";

        public string? Get(CollectionUrl collection) => _secrets.GetValueOrDefault(collection);

        public void Set(CollectionUrl collection, string secret) => _secrets[collection] = secret;

        public bool Remove(CollectionUrl collection) => _secrets.Remove(collection);
    }
}
