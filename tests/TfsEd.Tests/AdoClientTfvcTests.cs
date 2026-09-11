using System.IO.Compression;
using System.Net;
using System.Text;
using TfsEd.Core;
using TfsEd.Core.Tfvc;
using TfsEd.Server;

namespace TfsEd.Tests;

public class AdoClientTfvcTests
{
    private static readonly CollectionUrl Collection = CollectionUrl.Parse("https://tfs.example.com/DefaultCollection");

    [Fact]
    public async Task GetItems_sends_version_and_parses_items()
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.Json(
            """{"count":2,"value":[{"version":5,"path":"$/P","isFolder":true},{"version":4,"size":3,"hashValue":"h==","path":"$/P/a.txt","changeDate":"2025-01-01T00:00:00Z"}]}"""));
        var client = new AdoClient(new HttpClient(handler), Collection, "pat");

        var items = await client.GetItemsAsync("$/P", new VersionSpec(5), RecursionLevel.Full, CancellationToken.None);

        var query = Uri.UnescapeDataString(Assert.Single(handler.Requests).RequestUri!.Query);
        Assert.Contains("scopePath=$/P", query);
        Assert.Contains("recursionLevel=Full", query);
        Assert.Contains("versionDescriptor.version=5", query);
        Assert.True(items[0].IsFolder);
        Assert.Equal(new ServerItem("$/P/a.txt", 4, false, 3, "h==", DateTimeOffset.Parse("2025-01-01T00:00:00Z")), items[1]);
    }

    [Fact]
    public async Task Download_extracts_files_from_item_batch_zip()
    {
        var handler = new StubHttpHandler(_ => Zip(("$/P/a.txt", "alpha"), ("$/P/ü.txt", "umlaut")));
        var client = new AdoClient(new HttpClient(handler), Collection, "pat");
        var files = new[] { File("$/P/a.txt"), File("$/P/Ü.txt") };
        var received = new Dictionary<string, string>();

        await client.DownloadAsync(files, async (item, content, token) =>
        {
            using var reader = new StreamReader(content);
            received[item.Path] = await reader.ReadToEndAsync(token);
        }, CancellationToken.None);

        Assert.Equal("alpha", received["$/P/a.txt"]);
        Assert.Equal("umlaut", received["$/P/Ü.txt"]);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("/_apis/tfvc/itembatch", request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Download_fails_if_the_server_omits_a_file()
    {
        var handler = new StubHttpHandler(_ => Zip(("$/P/a.txt", "alpha")));
        var client = new AdoClient(new HttpClient(handler), Collection, "pat");

        await Assert.ThrowsAsync<TfsEdServerException>(() =>
            client.DownloadAsync([File("$/P/a.txt"), File("$/P/b.txt")], (_, _, _) => Task.CompletedTask, CancellationToken.None));
    }

    [Fact]
    public async Task Missing_item_maps_to_not_found()
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.Json("""{"message":"TF401174: not found"}""", HttpStatusCode.NotFound));
        var client = new AdoClient(new HttpClient(handler), Collection, "pat");

        var ex = await Assert.ThrowsAsync<TfsEdServerException>(() =>
            client.GetItemsAsync("$/Nope", VersionSpec.Latest, RecursionLevel.None, CancellationToken.None));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    private static ServerItem File(string path) => new(path, 1, false, 5, null, DateTimeOffset.UnixEpoch);

    private static HttpResponseMessage Zip(params (string Name, string Content)[] entries)
    {
        var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: Encoding.UTF8))
        {
            foreach (var (name, content) in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write(content);
            }
        }

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(buffer.ToArray()) };
    }
}
