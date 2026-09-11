using TfsEd.Core.Tfvc;

namespace TfsEd.Core.Workspaces;

public enum ChangeKind
{
    Modified,
    Missing,
    Untracked,
}

public sealed record LocalChange(ChangeKind Kind, string ServerPath, string LocalPath, bool IsFolder);

/// <summary>Detects local changes against the baseline without contacting the server.</summary>
public static class StatusScanner
{
    private static readonly EnumerationOptions Enumeration = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
    };

    public static IReadOnlyList<LocalChange> Scan(Workspace workspace, string scope)
    {
        var changes = new List<LocalChange>();
        foreach (var entry in workspace.Baseline.EntriesUnder(scope))
        {
            var local = workspace.ToLocalPath(entry.ServerPath);
            if (entry.IsFolder)
            {
                if (!Directory.Exists(local))
                {
                    changes.Add(new LocalChange(ChangeKind.Missing, entry.ServerPath, local, IsFolder: true));
                }

                continue;
            }

            var state = LocalFileState.Check(entry, local);
            if (state != LocalFileStatus.Unchanged)
            {
                var kind = state == LocalFileStatus.Missing ? ChangeKind.Missing : ChangeKind.Modified;
                changes.Add(new LocalChange(kind, entry.ServerPath, local, IsFolder: false));
            }
        }

        var scopeLocal = workspace.ToLocalPath(scope);
        if (Directory.Exists(scopeLocal))
        {
            ScanDirectory(workspace, scopeLocal, scope, IgnoreMatcher.ForPath(workspace.Root, scopeLocal), changes);
        }
        else if (File.Exists(scopeLocal) && workspace.Baseline.Get(scope) is null)
        {
            changes.Add(new LocalChange(ChangeKind.Untracked, scope, scopeLocal, IsFolder: false));
        }

        changes.Sort((a, b) => ServerPath.Comparer.Compare(a.ServerPath, b.ServerPath));
        return changes;
    }

    private static void ScanDirectory(Workspace workspace, string directory, string serverDirectory, IgnoreMatcher matcher, List<LocalChange> changes)
    {
        var isRoot = ServerPath.AreEqual(serverDirectory, workspace.ServerRoot);
        foreach (var info in new DirectoryInfo(directory).EnumerateFileSystemInfos("*", Enumeration))
        {
            var isDirectory = info is DirectoryInfo;
            if ((isRoot && isDirectory && info.Name == Workspace.MetadataDirectoryName)
                || (!isDirectory && info.Name.EndsWith(Workspace.TempFileSuffix, StringComparison.Ordinal)))
            {
                continue;
            }

            var serverPath = ServerPath.Combine(serverDirectory, info.Name);
            var entry = workspace.Baseline.Get(serverPath);
            var relative = ServerPath.GetRelative(workspace.ServerRoot, serverPath);

            // Items under version control are reported regardless of ignore rules.
            if (entry is null && matcher.IsIgnored(relative))
            {
                continue;
            }

            if (!isDirectory)
            {
                if (entry is null)
                {
                    changes.Add(new LocalChange(ChangeKind.Untracked, serverPath, info.FullName, IsFolder: false));
                }
            }
            else if (entry is null)
            {
                changes.Add(new LocalChange(ChangeKind.Untracked, serverPath, info.FullName, IsFolder: true));
            }
            else if (info.LinkTarget is null)
            {
                ScanDirectory(workspace, info.FullName, serverPath, matcher.ForDirectory(info.FullName, relative), changes);
            }
        }
    }
}
