namespace TfsEd.Core.Credentials;

/// <summary>Stores personal access tokens per collection.</summary>
public interface ICredentialStore
{
    /// <summary>Human-readable name of the backing store, e.g. "macOS Keychain".</summary>
    string Description { get; }

    string? Get(CollectionUrl collection);

    void Set(CollectionUrl collection, string secret);

    /// <returns><c>true</c> if a credential was removed.</returns>
    bool Remove(CollectionUrl collection);
}

public sealed class CredentialStoreException(string message) : TfsEdException(message);
