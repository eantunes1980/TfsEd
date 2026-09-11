using System.Text.Json;

namespace TfsEd.Core.Credentials;

/// <summary>
/// Plain JSON file readable only by the current user (mode 0600). Fallback for systems without a keychain,
/// e.g. headless Linux servers.
/// </summary>
public sealed class FileCredentialStore : ICredentialStore
{
    private readonly string _path;

    public FileCredentialStore(string path) => _path = path;

    public string Description => $"file {_path} (no OS keychain available)";

    public string? Get(CollectionUrl collection) =>
        Load().TryGetValue(collection.Value, out var secret) ? secret : null;

    public void Set(CollectionUrl collection, string secret)
    {
        var entries = Load();
        entries[collection.Value] = secret;
        Save(entries);
    }

    public bool Remove(CollectionUrl collection)
    {
        var entries = Load();
        if (!entries.Remove(collection.Value))
        {
            return false;
        }

        Save(entries);
        return true;
    }

    private Dictionary<string, string> Load()
    {
        if (!File.Exists(_path))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var loaded = JsonSerializer.Deserialize(File.ReadAllText(_path), CoreJsonContext.Default.DictionaryStringString);
        return new Dictionary<string, string>(loaded ?? [], StringComparer.OrdinalIgnoreCase);
    }

    private void Save(Dictionary<string, string> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using var stream = new FileStream(_path, options);
        JsonSerializer.Serialize(stream, entries, CoreJsonContext.Default.DictionaryStringString);
    }
}
