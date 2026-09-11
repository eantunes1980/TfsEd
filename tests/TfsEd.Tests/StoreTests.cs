using TfsEd.Core;
using TfsEd.Core.Configuration;
using TfsEd.Core.Credentials;

namespace TfsEd.Tests;

public sealed class StoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("tfsed-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void ConfigStore_returns_empty_config_when_missing() =>
        Assert.Null(new ConfigStore(Path.Combine(_dir, "config.json")).Load().DefaultCollection);

    [Fact]
    public void ConfigStore_round_trips()
    {
        var store = new ConfigStore(Path.Combine(_dir, "sub", "config.json"));
        store.Save(new AppConfig { DefaultCollection = "https://tfs.example.com/Coll", CaCertificatePath = "/ca.pem" });

        var loaded = store.Load();

        Assert.Equal("https://tfs.example.com/Coll", loaded.DefaultCollection);
        Assert.Equal("/ca.pem", loaded.CaCertificatePath);
    }

    [Fact]
    public void FileCredentialStore_sets_gets_and_removes()
    {
        var store = new FileCredentialStore(Path.Combine(_dir, "credentials.json"));
        var collection = CollectionUrl.Parse("https://tfs.example.com/Coll");

        store.Set(collection, "secret");

        Assert.Equal("secret", store.Get(CollectionUrl.Parse("https://TFS.example.com/Coll")));
        Assert.True(store.Remove(collection));
        Assert.False(store.Remove(collection));
        Assert.Null(store.Get(collection));
    }

    [Fact]
    public void FileCredentialStore_is_user_only_on_unix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var path = Path.Combine(_dir, "credentials.json");
        new FileCredentialStore(path).Set(CollectionUrl.Parse("https://tfs.example.com/Coll"), "secret");

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }
}
