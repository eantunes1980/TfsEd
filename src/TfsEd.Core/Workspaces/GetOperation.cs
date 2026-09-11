using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using TfsEd.Core.Tfvc;

namespace TfsEd.Core.Workspaces;

public sealed record GetOptions(string Scope, VersionSpec Version, bool Force = false, bool Preview = false);

public enum GetActionKind
{
    Get,
    Replace,
    Delete,
    Conflict,
}

public sealed record GetAction(GetActionKind Kind, string ServerPath, string LocalPath, string? Detail = null);

public sealed record GetResult(int Version, IReadOnlyList<GetAction> Actions)
{
    public int Count(GetActionKind kind) => Actions.Count(action => action.Kind == kind);
}

/// <summary>
/// Synchronizes a workspace scope with a server version. Local modifications are never overwritten
/// unless <see cref="GetOptions.Force"/> is set; they are reported as conflicts instead.
/// </summary>
public sealed class GetOperation(Workspace workspace, ITfvcServer server)
{
    public async Task<GetResult> RunAsync(GetOptions options, CancellationToken cancellationToken)
    {
        var target = options.Version.Changeset ?? await server.GetLatestChangesetIdAsync(cancellationToken);
        var serverItems = await GetServerItemsAsync(options.Scope, target, cancellationToken);
        var serverByPath = new Dictionary<string, ServerItem>(ServerPath.Comparer);
        foreach (var item in serverItems)
        {
            serverByPath[item.Path] = item;
        }

        var actions = new List<GetAction>();
        var downloads = new List<ServerItem>();
        var adopted = new List<BaselineEntry>();
        var folders = new List<ServerItem>();
        PlanServerItems(serverItems, options.Force, actions, downloads, adopted, folders);

        var fileDeletions = new List<BaselineEntry>();
        var folderDeletions = new List<BaselineEntry>();
        var staleEntries = new List<BaselineEntry>();
        foreach (var entry in workspace.Baseline.EntriesUnder(options.Scope))
        {
            if (serverByPath.TryGetValue(entry.ServerPath, out var item))
            {
                if (item.IsFolder != entry.IsFolder)
                {
                    staleEntries.Add(entry);
                }

                continue;
            }

            if (entry.IsFolder)
            {
                folderDeletions.Add(entry);
                continue;
            }

            var local = workspace.ToLocalPath(entry.ServerPath);
            var state = LocalFileState.Check(entry, local);
            if (state == LocalFileStatus.Modified && !options.Force)
            {
                actions.Add(new GetAction(GetActionKind.Conflict, entry.ServerPath, local, "deleted on the server but modified locally"));
                continue;
            }

            fileDeletions.Add(entry);
            if (state != LocalFileStatus.Missing)
            {
                actions.Add(new GetAction(GetActionKind.Delete, entry.ServerPath, local));
            }
        }

        if (options.Preview)
        {
            return new GetResult(target, actions);
        }

        try
        {
            foreach (var entry in staleEntries)
            {
                workspace.Baseline.Remove(entry.ServerPath);
            }

            foreach (var folder in folders)
            {
                Directory.CreateDirectory(workspace.ToLocalPath(folder.Path));
                workspace.Baseline.Set(new BaselineEntry(folder.Path, true, folder.Version, 0, null, 0));
            }

            foreach (var entry in adopted)
            {
                workspace.Baseline.Set(entry);
            }

            await DownloadAsync(downloads, cancellationToken);
            DeleteFiles(fileDeletions);
            DeleteFolders(folderDeletions);

            if (ServerPath.AreEqual(options.Scope, workspace.ServerRoot))
            {
                workspace.Info.Version = target;
            }
        }
        finally
        {
            // Persist whatever was completed so an interrupted get can simply be repeated.
            workspace.Save();
        }

        return new GetResult(target, actions);
    }

    private void PlanServerItems(
        IReadOnlyList<ServerItem> serverItems,
        bool force,
        List<GetAction> actions,
        List<ServerItem> downloads,
        List<BaselineEntry> adopted,
        List<ServerItem> folders)
    {
        foreach (var item in serverItems)
        {
            if (item.IsFolder)
            {
                folders.Add(item);
                continue;
            }

            var local = workspace.ToLocalPath(item.Path);
            var entry = workspace.Baseline.Get(item.Path);
            if (entry is { IsFolder: false })
            {
                var sameVersion = entry.Version == item.Version;
                if (sameVersion && !force)
                {
                    continue;
                }

                var state = LocalFileState.Check(entry, local);
                if (sameVersion && state == LocalFileStatus.Unchanged)
                {
                    continue;
                }

                if (state == LocalFileStatus.Modified && !force)
                {
                    actions.Add(new GetAction(GetActionKind.Conflict, item.Path, local, "local file is modified"));
                    continue;
                }

                downloads.Add(item);
                actions.Add(new GetAction(state == LocalFileStatus.Missing ? GetActionKind.Get : GetActionKind.Replace, item.Path, local));
                continue;
            }

            if (File.Exists(local) && !force)
            {
                // A file identical to the server version (e.g. from an earlier interrupted get) is simply adopted.
                if (item.Hash is not null && FileHasher.Md5Base64(local) == item.Hash)
                {
                    var info = new FileInfo(local);
                    adopted.Add(new BaselineEntry(item.Path, false, item.Version, info.Length, item.Hash, info.LastWriteTimeUtc.Ticks));
                }
                else
                {
                    actions.Add(new GetAction(GetActionKind.Conflict, item.Path, local, "a local file that is not under version control exists"));
                }

                continue;
            }

            downloads.Add(item);
            actions.Add(new GetAction(GetActionKind.Get, item.Path, local));
        }
    }

    private async Task<IReadOnlyList<ServerItem>> GetServerItemsAsync(string scope, int version, CancellationToken cancellationToken)
    {
        try
        {
            return await server.GetItemsAsync(scope, new VersionSpec(version), RecursionLevel.Full, cancellationToken);
        }
        catch (TfsEdException ex) when (ex is IHasStatusCode { StatusCode: HttpStatusCode.NotFound })
        {
            // The scope does not exist at this version: everything below it is deleted locally.
            return [];
        }
    }

    private async Task DownloadAsync(List<ServerItem> files, CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            return;
        }

        var completed = new ConcurrentBag<BaselineEntry>();
        try
        {
            await server.DownloadAsync(
                files,
                async (item, content, token) => completed.Add(await WriteFileAsync(item, content, token)),
                cancellationToken);
        }
        finally
        {
            foreach (var entry in completed)
            {
                workspace.Baseline.Set(entry);
            }
        }
    }

    private async Task<BaselineEntry> WriteFileAsync(ServerItem item, Stream content, CancellationToken cancellationToken)
    {
        var local = workspace.ToLocalPath(item.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(local)!);
        var temp = local + Workspace.TempFileSuffix;

        string hash;
        await using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
            var buffer = new byte[81920];
            int read;
            while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                md5.AppendData(buffer, 0, read);
                await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            hash = Convert.ToBase64String(md5.GetHashAndReset());
        }

        if (item.Hash is not null && item.Hash != hash)
        {
            File.Delete(temp);
            throw new TfsEdException($"Checksum mismatch for {item.Path}; the download is corrupt.");
        }

        if (File.Exists(local))
        {
            File.SetAttributes(local, FileAttributes.Normal);
        }

        File.Move(temp, local, overwrite: true);
        var info = new FileInfo(local);
        return new BaselineEntry(item.Path, false, item.Version, info.Length, hash, info.LastWriteTimeUtc.Ticks);
    }

    private void DeleteFiles(List<BaselineEntry> entries)
    {
        foreach (var entry in entries)
        {
            var local = workspace.ToLocalPath(entry.ServerPath);
            if (File.Exists(local))
            {
                File.SetAttributes(local, FileAttributes.Normal);
                File.Delete(local);
            }

            workspace.Baseline.Remove(entry.ServerPath);
        }
    }

    private void DeleteFolders(List<BaselineEntry> entries)
    {
        // Deepest first; folders that still contain untracked files are kept.
        foreach (var entry in entries.OrderByDescending(entry => entry.ServerPath.Length))
        {
            var local = workspace.ToLocalPath(entry.ServerPath);
            if (Directory.Exists(local) && !Directory.EnumerateFileSystemEntries(local).Any())
            {
                Directory.Delete(local);
            }

            workspace.Baseline.Remove(entry.ServerPath);
        }
    }
}

/// <summary>Implemented by server exceptions so Core can react to HTTP status codes without depending on the REST layer.</summary>
public interface IHasStatusCode
{
    HttpStatusCode? StatusCode { get; }
}
