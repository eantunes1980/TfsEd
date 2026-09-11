using System.Security.Cryptography;

namespace TfsEd.Core.Workspaces;

public enum LocalFileStatus
{
    Unchanged,
    Modified,
    Missing,
}

public static class LocalFileState
{
    /// <summary>
    /// Compares a local file with its baseline: size and timestamp as a fast path, content hash otherwise.
    /// </summary>
    public static LocalFileStatus Check(BaselineEntry entry, string localPath)
    {
        var info = new FileInfo(localPath);
        if (!info.Exists)
        {
            return LocalFileStatus.Missing;
        }

        if (info.Length != entry.Size)
        {
            return LocalFileStatus.Modified;
        }

        if (info.LastWriteTimeUtc.Ticks == entry.LastWriteTicks)
        {
            return LocalFileStatus.Unchanged;
        }

        return entry.Hash is not null && FileHasher.Md5Base64(localPath) == entry.Hash
            ? LocalFileStatus.Unchanged
            : LocalFileStatus.Modified;
    }
}

public static class FileHasher
{
    /// <summary>Base64 MD5, the same format as the server's <c>hashValue</c>.</summary>
    public static string Md5Base64(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToBase64String(MD5.HashData(stream));
    }
}
