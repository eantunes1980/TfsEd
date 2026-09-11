using System.Net;
using System.Security.Cryptography;
using System.Text;
using TfsEd.Core.Tfvc;
using TfsEd.Server;

namespace TfsEd.Tests;

/// <summary>In-memory TFVC server built from full tree snapshots per changeset.</summary>
internal sealed class FakeTfvcServer : ITfvcServer
{
    private readonly SortedDictionary<int, Dictionary<string, byte[]?>> _snapshots = [];

    public int DownloadedFiles { get; private set; }

    /// <summary>Adds a changeset; <c>null</c> content denotes a folder.</summary>
    public void Commit(int changeset, params (string Path, string? Content)[] tree)
    {
        var snapshot = new Dictionary<string, byte[]?>(ServerPath.Comparer);
        foreach (var (path, content) in tree)
        {
            snapshot[path] = content is null ? null : Encoding.UTF8.GetBytes(content);
        }

        _snapshots[changeset] = snapshot;
    }

    public Task<int> GetLatestChangesetIdAsync(CancellationToken cancellationToken) => Task.FromResult(_snapshots.Keys.Max());

    public Task<IReadOnlyList<ServerItem>> GetItemsAsync(string serverPath, VersionSpec version, RecursionLevel recursion, CancellationToken cancellationToken)
    {
        var changeset = version.Changeset ?? _snapshots.Keys.Max();
        var snapshot = Snapshot(changeset);
        if (!snapshot.ContainsKey(serverPath))
        {
            throw new TfsEdServerException($"TF401174: {serverPath} not found", HttpStatusCode.NotFound);
        }

        var items = snapshot
            .Where(pair => recursion switch
            {
                RecursionLevel.None => ServerPath.AreEqual(pair.Key, serverPath),
                RecursionLevel.OneLevel => ServerPath.IsSameOrUnder(pair.Key, serverPath)
                    && !ServerPath.GetRelative(serverPath, pair.Key).Contains('/'),
                _ => ServerPath.IsSameOrUnder(pair.Key, serverPath),
            })
            .Select(pair => new ServerItem(
                pair.Key,
                ItemVersion(pair.Key, changeset),
                pair.Value is null,
                pair.Value?.Length ?? 0,
                pair.Value is null ? null : Convert.ToBase64String(MD5.HashData(pair.Value)),
                DateTimeOffset.UnixEpoch))
            .ToList();
        return Task.FromResult<IReadOnlyList<ServerItem>>(items);
    }

    public async Task DownloadAsync(IReadOnlyCollection<ServerItem> files, Func<ServerItem, Stream, CancellationToken, Task> consume, CancellationToken cancellationToken)
    {
        foreach (var file in files)
        {
            await consume(file, new MemoryStream(Snapshot(file.Version)[file.Path]!), cancellationToken);
            DownloadedFiles++;
        }
    }

    public Task<byte[]> GetContentAsync(string serverPath, int version, CancellationToken cancellationToken) =>
        Task.FromResult(Snapshot(version)[serverPath]!);

    public Task<IReadOnlyList<ChangesetSummary>> GetHistoryAsync(string serverPath, int top, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ChangesetDetails> GetChangesetAsync(int id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    private Dictionary<string, byte[]?> Snapshot(int version) => _snapshots.Last(pair => pair.Key <= version).Value;

    /// <summary>The oldest changeset since which the item is unchanged.</summary>
    private int ItemVersion(string path, int version)
    {
        var current = Snapshot(version)[path];
        var result = version;
        foreach (var (changeset, snapshot) in _snapshots.Where(pair => pair.Key <= version).Reverse())
        {
            if (!snapshot.TryGetValue(path, out var content)
                || (content is null) != (current is null)
                || (content is not null && !content.AsSpan().SequenceEqual(current)))
            {
                break;
            }

            result = changeset;
        }

        return result;
    }
}
