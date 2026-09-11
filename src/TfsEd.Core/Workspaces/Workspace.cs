using System.Text.Json;
using TfsEd.Core.Tfvc;

namespace TfsEd.Core.Workspaces;

/// <summary>
/// A local workspace: one server folder mapped to one local directory. All state lives in the
/// <c>.tf</c> directory at the workspace root; no server-side workspace is created.
/// </summary>
public sealed class Workspace
{
    public const string MetadataDirectoryName = ".tf";
    public const string TempFileSuffix = ".tfsed-tmp";

    private const string InfoFileName = "workspace.json";
    private const string BaselineFileName = "baseline.tsv";

    private static readonly StringComparison LocalPathComparison =
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    private Workspace(string root, WorkspaceInfo info, Baseline baseline)
    {
        Root = root;
        Info = info;
        Baseline = baseline;
    }

    /// <summary>Full local path of the workspace root.</summary>
    public string Root { get; }

    public WorkspaceInfo Info { get; }

    public Baseline Baseline { get; }

    public string ServerRoot => Info.ServerPath;

    public CollectionUrl Collection => CollectionUrl.Parse(Info.Collection);

    private string MetadataDirectory => Path.Combine(Root, MetadataDirectoryName);

    public static Workspace Create(string root, CollectionUrl collection, string serverPath)
    {
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var existing = Find(root);
        if (existing is not null)
        {
            throw new TfsEdException($"'{root}' is already inside the workspace '{existing.Root}'.");
        }

        var metadata = Directory.CreateDirectory(Path.Combine(root, MetadataDirectoryName));
        if (OperatingSystem.IsWindows())
        {
            metadata.Attributes |= FileAttributes.Hidden;
        }

        var info = new WorkspaceInfo { Collection = collection.Value, ServerPath = ServerPath.Normalize(serverPath) };
        var workspace = new Workspace(root, info, new Baseline());
        workspace.Save();
        return workspace;
    }

    /// <summary>Finds the workspace containing <paramref name="path"/> (a file or directory), or <c>null</c>.</summary>
    public static Workspace? Find(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = File.Exists(fullPath) ? new FileInfo(fullPath).Directory : new DirectoryInfo(fullPath);
        for (var current = directory; current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, MetadataDirectoryName, InfoFileName)))
            {
                return Load(current.FullName);
            }
        }

        return null;
    }

    public static Workspace Load(string root)
    {
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var metadata = Path.Combine(root, MetadataDirectoryName);
        var infoFile = Path.Combine(metadata, InfoFileName);
        WorkspaceInfo? info;
        try
        {
            info = JsonSerializer.Deserialize(File.ReadAllText(infoFile), CoreJsonContext.Default.WorkspaceInfo);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            throw new TfsEdException($"Could not read workspace metadata '{infoFile}': {ex.Message}", ex);
        }

        if (info is null || info.ServerPath.Length == 0)
        {
            throw new TfsEdException($"Workspace metadata '{infoFile}' is corrupt.");
        }

        return new Workspace(root, info, Baseline.Load(Path.Combine(metadata, BaselineFileName)));
    }

    public void Save()
    {
        var infoFile = Path.Combine(MetadataDirectory, InfoFileName);
        File.WriteAllText(infoFile + ".tmp", JsonSerializer.Serialize(Info, CoreJsonContext.Default.WorkspaceInfo));
        File.Move(infoFile + ".tmp", infoFile, overwrite: true);
        Baseline.Save(Path.Combine(MetadataDirectory, BaselineFileName));
    }

    /// <summary>Removes the workspace metadata; working files are kept.</summary>
    public void DeleteMetadata() => Directory.Delete(MetadataDirectory, recursive: true);

    public bool Contains(string localPath)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(localPath));
        if (fullPath.Equals(Root, LocalPathComparison))
        {
            return true;
        }

        var prefix = Path.EndsInDirectorySeparator(Root) ? Root : Root + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(prefix, LocalPathComparison);
    }

    public string ToLocalPath(string serverPath)
    {
        var relative = ServerPath.GetRelative(ServerRoot, serverPath);
        return relative.Length == 0 ? Root : Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    public string ToServerPath(string localPath)
    {
        if (!Contains(localPath))
        {
            throw new TfsEdException($"'{localPath}' is not inside the workspace '{Root}'.");
        }

        var relative = Path.GetRelativePath(Root, Path.GetFullPath(localPath));
        return relative == "."
            ? ServerRoot
            : ServerPath.Combine(ServerRoot, relative.Replace(Path.DirectorySeparatorChar, '/').TrimEnd('/'));
    }
}
