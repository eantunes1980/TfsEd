using TfsEd.Core.Configuration;

namespace TfsEd.Core.Credentials;

public static class CredentialStoreFactory
{
    internal const string ServiceName = "tfsed";

    /// <summary>Picks the best available store for the current OS.</summary>
    public static ICredentialStore CreateDefault(AppPaths paths)
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsCredentialStore();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacKeychainCredentialStore();
        }

        // libsecret needs a D-Bus session; headless/SSH sessions fall back to a user-only file.
        if (ProcessRunner.IsOnPath("secret-tool")
            && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS")))
        {
            return new SecretToolCredentialStore();
        }

        return new FileCredentialStore(paths.CredentialsFile);
    }
}
