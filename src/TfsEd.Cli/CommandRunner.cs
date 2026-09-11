using System.Security.Authentication;
using TfsEd.Core;

namespace TfsEd.Cli;

/// <summary>Turns expected failures into a one-line error message and exit code 1.</summary>
internal static class CommandRunner
{
    public static async Task<int> RunAsync(CliServices services, Func<Task<int>> action)
    {
        try
        {
            return await action();
        }
        catch (TfsEdException ex)
        {
            await services.Error.WriteLineAsync($"error: {ex.Message}");
        }
        catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException)
        {
            await services.Error.WriteLineAsync(
                "error: the server's TLS certificate is not trusted. Install the issuing root CA in the system store, "
                + "or run 'tfsed login <collection-url> --ca-cert <root-ca.pem>'.");
        }
        catch (HttpRequestException ex)
        {
            await services.Error.WriteLineAsync($"error: could not reach the server: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            await services.Error.WriteLineAsync("error: the request timed out or was cancelled.");
        }

        return 1;
    }
}
