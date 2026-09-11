namespace TfsEd.Core.Credentials;

/// <summary>Linux Secret Service (GNOME Keyring, KWallet) via <c>secret-tool</c> from libsecret.</summary>
public sealed class SecretToolCredentialStore : ICredentialStore
{
    private const string Tool = "secret-tool";

    public string Description => "Secret Service (libsecret)";

    public string? Get(CollectionUrl collection)
    {
        // secret-tool exits with 1 and no output when nothing matches.
        var result = ProcessRunner.Run(Tool, ["lookup", .. Attributes(collection)]);
        return result.ExitCode == 0 && result.StandardOutput.Length > 0 ? result.StandardOutput.TrimEnd('\n') : null;
    }

    public void Set(CollectionUrl collection, string secret)
    {
        // The secret is passed on stdin so it never appears in the process list.
        var result = ProcessRunner.Run(Tool, ["store", $"--label=TfsEd {collection}", .. Attributes(collection)], secret);
        if (result.ExitCode != 0)
        {
            throw new CredentialStoreException($"secret-tool store failed: {result.StandardError.Trim()}");
        }
    }

    public bool Remove(CollectionUrl collection)
    {
        var existed = Get(collection) is not null;
        ProcessRunner.Run(Tool, ["clear", .. Attributes(collection)]);
        return existed;
    }

    private static string[] Attributes(CollectionUrl collection) =>
        ["service", CredentialStoreFactory.ServiceName, "collection", collection.Value];
}
