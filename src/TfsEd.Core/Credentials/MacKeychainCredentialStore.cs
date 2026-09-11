namespace TfsEd.Core.Credentials;

/// <summary>macOS login keychain via the <c>security</c> tool (service <c>tfsed</c>, account = collection URL).</summary>
public sealed class MacKeychainCredentialStore : ICredentialStore
{
    private const string Tool = "/usr/bin/security";
    private const int ItemNotFound = 44;

    public string Description => "macOS Keychain";

    public string? Get(CollectionUrl collection)
    {
        var result = ProcessRunner.Run(Tool, ["find-generic-password", "-s", CredentialStoreFactory.ServiceName, "-a", collection.Value, "-w"]);
        if (result.ExitCode == ItemNotFound)
        {
            return null;
        }

        EnsureSuccess(result, "read");
        return result.StandardOutput.TrimEnd('\r', '\n');
    }

    public void Set(CollectionUrl collection, string secret)
    {
        var result = ProcessRunner.Run(Tool, ["add-generic-password", "-U", "-s", CredentialStoreFactory.ServiceName, "-a", collection.Value, "-l", $"TfsEd {collection}", "-w", secret]);
        EnsureSuccess(result, "store");
    }

    public bool Remove(CollectionUrl collection)
    {
        var result = ProcessRunner.Run(Tool, ["delete-generic-password", "-s", CredentialStoreFactory.ServiceName, "-a", collection.Value]);
        if (result.ExitCode == ItemNotFound)
        {
            return false;
        }

        EnsureSuccess(result, "delete");
        return true;
    }

    private static void EnsureSuccess(ProcessResult result, string operation)
    {
        if (result.ExitCode != 0)
        {
            throw new CredentialStoreException($"Could not {operation} keychain item: {result.StandardError.Trim()}");
        }
    }
}
