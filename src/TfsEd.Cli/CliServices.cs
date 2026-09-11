using TfsEd.Core.Configuration;
using TfsEd.Core.Credentials;
using TfsEd.Server;

namespace TfsEd.Cli;

/// <summary>Dependencies of all commands; replaced by fakes in tests.</summary>
public sealed class CliServices
{
    public required ConfigStore Config { get; init; }

    public required ICredentialStore Credentials { get; init; }

    /// <summary>Creates an HTTP client; the argument is the optional extra CA certificate path.</summary>
    public required Func<string?, HttpClient> CreateHttpClient { get; init; }

    public Func<string, string?> GetEnvironmentVariable { get; init; } = Environment.GetEnvironmentVariable;

    public TextReader Input { get; init; } = Console.In;

    public TextWriter Output { get; init; } = Console.Out;

    public TextWriter Error { get; init; } = Console.Error;

    public bool IsInteractive { get; init; } = !Console.IsInputRedirected;

    public static CliServices CreateDefault()
    {
        var paths = AppPaths.Default();
        return new CliServices
        {
            Config = new ConfigStore(paths.ConfigFile),
            Credentials = CredentialStoreFactory.CreateDefault(paths),
            CreateHttpClient = ServerHttpClientFactory.Create,
        };
    }
}
