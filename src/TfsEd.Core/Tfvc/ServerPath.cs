namespace TfsEd.Core.Tfvc;

/// <summary>Helpers for TFVC server paths such as <c>$/Project/Folder/File.cs</c> (case-insensitive).</summary>
public static class ServerPath
{
    public const string Root = "$/";

    public static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    public static bool IsServerPath(string value) => value.StartsWith("$/", StringComparison.Ordinal) || value == "$";

    /// <summary>Uses forward slashes, removes duplicate and trailing slashes.</summary>
    public static string Normalize(string path)
    {
        var normalized = path.Trim().Replace('\\', '/');
        if (!IsServerPath(normalized))
        {
            throw new TfsEdException($"'{path}' is not a server path (it must start with $/).");
        }

        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        normalized = normalized.TrimEnd('/');
        return normalized.Length <= 1 ? Root : normalized;
    }

    public static bool AreEqual(string a, string b) => Comparer.Equals(a, b);

    public static bool IsSameOrUnder(string path, string parent)
    {
        if (AreEqual(path, parent))
        {
            return true;
        }

        var prefix = parent == Root ? Root : parent + "/";
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Appends a '/'-separated relative path.</summary>
    public static string Combine(string parent, string relative)
    {
        if (relative.Length == 0)
        {
            return parent;
        }

        return parent == Root ? Root + relative : $"{parent}/{relative}";
    }

    /// <summary>Returns the '/'-separated path of <paramref name="path"/> below <paramref name="parent"/> ("" if equal).</summary>
    public static string GetRelative(string parent, string path)
    {
        if (!IsSameOrUnder(path, parent))
        {
            throw new TfsEdException($"'{path}' is not below '{parent}'.");
        }

        if (AreEqual(path, parent))
        {
            return string.Empty;
        }

        return path[(parent == Root ? Root.Length : parent.Length + 1)..];
    }

    public static string GetFileName(string path)
    {
        var index = path.LastIndexOf('/');
        return path == Root ? string.Empty : path[(index + 1)..];
    }
}
