namespace TfsEd.Core.Tfvc;

public enum RecursionLevel
{
    None,
    OneLevel,
    Full,
}

public sealed record ServerItem(string Path, int Version, bool IsFolder, long Size, string? Hash, DateTimeOffset ChangeDate);

public sealed record ChangesetSummary(int Id, string Author, string? AuthorUniqueName, DateTimeOffset Date, string? Comment);

public sealed record ItemChange(string Path, string ChangeType, int Version);

public sealed record ChangesetDetails(ChangesetSummary Summary, IReadOnlyList<ItemChange> Changes);

/// <summary>Server operations needed by the workspace logic; implemented by the REST client.</summary>
public interface ITfvcServer
{
    Task<int> GetLatestChangesetIdAsync(CancellationToken cancellationToken);

    /// <summary>Lists items; throws a server exception with status 404 if the path does not exist at that version.</summary>
    Task<IReadOnlyList<ServerItem>> GetItemsAsync(string serverPath, VersionSpec version, RecursionLevel recursion, CancellationToken cancellationToken);

    /// <summary>
    /// Downloads the content of the given files at their <see cref="ServerItem.Version"/>.
    /// <paramref name="consume"/> is called once per file and may be called concurrently for different files.
    /// </summary>
    Task DownloadAsync(IReadOnlyCollection<ServerItem> files, Func<ServerItem, Stream, CancellationToken, Task> consume, CancellationToken cancellationToken);

    Task<byte[]> GetContentAsync(string serverPath, int version, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChangesetSummary>> GetHistoryAsync(string serverPath, int top, CancellationToken cancellationToken);

    Task<ChangesetDetails> GetChangesetAsync(int id, CancellationToken cancellationToken);
}
