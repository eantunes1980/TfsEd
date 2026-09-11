using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using TfsEd.Core;
using TfsEd.Core.Tfvc;

namespace TfsEd.Server;

/// <summary>Thin REST client for one Azure DevOps Server / TFS collection, authenticated with a PAT.</summary>
public sealed class AdoClient : ITfvcServer
{
    private const string ApiVersion = "7.0";
    private const int BatchMaxItems = 500;
    private const long BatchMaxBytes = 64L * 1024 * 1024;
    private const int MaxParallelDownloads = 4;
    private const int ChangesPageSize = 1000;

    private readonly HttpClient _http;
    private readonly AuthenticationHeaderValue _authorization;

    public AdoClient(HttpClient http, CollectionUrl collection, string personalAccessToken)
    {
        _http = http;
        Collection = collection;
        _authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{personalAccessToken}")));
    }

    public CollectionUrl Collection { get; }

    public Task<ConnectionData> GetConnectionDataAsync(CancellationToken cancellationToken) =>
        GetJsonAsync("_apis/connectionData?connectOptions=none", ServerJsonContext.Default.ConnectionData, cancellationToken);

    public async Task<IReadOnlyList<TeamProject>> GetProjectsAsync(CancellationToken cancellationToken)
    {
        var response = await GetJsonAsync(
            $"_apis/projects?$top=1000&api-version={ApiVersion}", ServerJsonContext.Default.ListResponseTeamProject, cancellationToken);
        return response.Value;
    }

    public async Task<int> GetLatestChangesetIdAsync(CancellationToken cancellationToken)
    {
        var response = await GetJsonAsync(
            $"_apis/tfvc/changesets?$top=1&api-version={ApiVersion}", ServerJsonContext.Default.ListResponseChangesetDto, cancellationToken);
        return response.Value.Count > 0
            ? response.Value[0].ChangesetId
            : throw new TfsEdServerException("The collection has no changesets.");
    }

    public async Task<IReadOnlyList<ServerItem>> GetItemsAsync(string serverPath, VersionSpec version, RecursionLevel recursion, CancellationToken cancellationToken)
    {
        var response = await GetJsonAsync(
            $"_apis/tfvc/items?scopePath={Escape(serverPath)}&recursionLevel={recursion}{VersionQuery(version)}&api-version={ApiVersion}",
            ServerJsonContext.Default.ListResponseTfvcItemDto,
            cancellationToken);
        return response.Value.Select(ToServerItem).ToList();
    }

    public async Task DownloadAsync(IReadOnlyCollection<ServerItem> files, Func<ServerItem, Stream, CancellationToken, Task> consume, CancellationToken cancellationToken)
    {
        var options = new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDownloads, CancellationToken = cancellationToken };
        await Parallel.ForEachAsync(CreateBatches(files), options, (batch, token) => new ValueTask(DownloadBatchAsync(batch, consume, token)));
    }

    public async Task<byte[]> GetContentAsync(string serverPath, int version, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            $"_apis/tfvc/items?path={Escape(serverPath)}{VersionQuery(new VersionSpec(version))}&download=true&api-version={ApiVersion}",
            "application/octet-stream");
        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, expectJson: false, cancellationToken);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChangesetSummary>> GetHistoryAsync(string serverPath, int top, CancellationToken cancellationToken)
    {
        var response = await GetJsonAsync(
            $"_apis/tfvc/changesets?searchCriteria.itemPath={Escape(serverPath)}&$top={top}&maxCommentLength=2000&api-version={ApiVersion}",
            ServerJsonContext.Default.ListResponseChangesetDto,
            cancellationToken);
        return response.Value.Select(ToSummary).ToList();
    }

    public async Task<ChangesetDetails> GetChangesetAsync(int id, CancellationToken cancellationToken)
    {
        var changeset = await GetJsonAsync(
            $"_apis/tfvc/changesets/{id}?maxCommentLength=100000&api-version={ApiVersion}", ServerJsonContext.Default.ChangesetDto, cancellationToken);

        var changes = new List<ItemChange>();
        for (var skip = 0; ; skip += ChangesPageSize)
        {
            var page = await GetJsonAsync(
                $"_apis/tfvc/changesets/{id}/changes?$top={ChangesPageSize}&$skip={skip}&api-version={ApiVersion}",
                ServerJsonContext.Default.ListResponseChangeDto,
                cancellationToken);
            changes.AddRange(page.Value.Select(change => new ItemChange(change.Item?.Path ?? string.Empty, change.ChangeType ?? string.Empty, change.Item?.Version ?? id)));
            if (page.Value.Count < ChangesPageSize)
            {
                break;
            }
        }

        return new ChangesetDetails(ToSummary(changeset), changes);
    }

    private async Task DownloadBatchAsync(List<ServerItem> batch, Func<ServerItem, Stream, CancellationToken, Task> consume, CancellationToken cancellationToken)
    {
        var body = new ItemBatchRequest
        {
            ItemDescriptors = batch
                .Select(item => new ItemDescriptorDto { Path = item.Path, Version = item.Version.ToString(CultureInfo.InvariantCulture) })
                .ToList(),
        };

        using var request = CreateRequest(HttpMethod.Post, $"_apis/tfvc/itembatch?api-version={ApiVersion}", "application/zip");
        request.Content = JsonContent.Create(body, ServerJsonContext.Default.ItemBatchRequest);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, expectJson: false, cancellationToken);

        // ZipArchive needs a seekable stream, so the archive is buffered in a self-deleting temp file.
        await using var buffer = new FileStream(
            Path.GetTempFileName(), FileMode.Open, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        await response.Content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var pending = batch.ToDictionary(item => item.Path, ServerPath.Comparer);
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true, entryNameEncoding: Encoding.UTF8))
        {
            foreach (var entry in zip.Entries)
            {
                var path = entry.FullName.Replace('\\', '/').TrimEnd('/');
                if (!pending.Remove(path, out var item))
                {
                    continue;
                }

                await using var content = entry.Open();
                await consume(item, content, cancellationToken);
            }
        }

        if (pending.Count > 0)
        {
            throw new TfsEdServerException($"The server did not return the content of {pending.Keys.First()}.");
        }
    }

    private static IEnumerable<List<ServerItem>> CreateBatches(IEnumerable<ServerItem> files)
    {
        var batch = new List<ServerItem>();
        long bytes = 0;
        foreach (var file in files)
        {
            if (batch.Count > 0 && (batch.Count >= BatchMaxItems || bytes + file.Size > BatchMaxBytes))
            {
                yield return batch;
                batch = [];
                bytes = 0;
            }

            batch.Add(file);
            bytes += file.Size;
        }

        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    private async Task<T> GetJsonAsync<T>(string relativeUrl, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, relativeUrl, "application/json");
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, expectJson: true, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken)
            ?? throw new TfsEdServerException($"Empty response from {request.RequestUri}.");
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl, string accept)
    {
        var request = new HttpRequestMessage(method, Collection.Combine(relativeUrl));
        request.Headers.Authorization = _authorization;
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, bool expectJson, CancellationToken cancellationToken)
    {
        // Azure DevOps answers a rejected PAT with 401, or with 203 + an HTML sign-in page.
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NonAuthoritativeInformation
            || (expectJson && response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType == "text/html"))
        {
            throw new TfsEdAuthenticationException(
                "Authentication failed: the personal access token is invalid, expired or lacks the required scopes.",
                response.StatusCode);
        }

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = TryReadErrorMessage(body) ?? response.ReasonPhrase;
        throw new TfsEdServerException($"Server returned {(int)response.StatusCode}: {message}", response.StatusCode);
    }

    private static string? TryReadErrorMessage(string body)
    {
        try
        {
            return JsonSerializer.Deserialize(body, ServerJsonContext.Default.ServerError)?.Message;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static string VersionQuery(VersionSpec version) =>
        version.IsLatest ? string.Empty : $"&versionDescriptor.version={version.Changeset}&versionDescriptor.versionType=changeset";

    private static ServerItem ToServerItem(TfvcItemDto item) =>
        new(item.Path, item.Version, item.IsFolder, item.Size, string.IsNullOrEmpty(item.HashValue) ? null : item.HashValue, item.ChangeDate);

    private static ChangesetSummary ToSummary(ChangesetDto changeset) =>
        new(changeset.ChangesetId, changeset.Author?.DisplayName ?? string.Empty, changeset.Author?.UniqueName, changeset.CreatedDate, changeset.Comment);
}
