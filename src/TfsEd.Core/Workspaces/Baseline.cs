using System.Globalization;
using System.Text;
using TfsEd.Core.Tfvc;

namespace TfsEd.Core.Workspaces;

/// <summary>What was last downloaded for an item: the server version plus the local file state right after writing it.</summary>
public sealed record BaselineEntry(string ServerPath, bool IsFolder, int Version, long Size, string? Hash, long LastWriteTicks);

/// <summary>
/// Item table of a workspace, stored as <c>.tf/baseline.tsv</c>
/// (one tab-separated line per item; TFVC paths cannot contain tabs).
/// </summary>
public sealed class Baseline
{
    private const string Header = "# tfsed baseline v1";

    private readonly Dictionary<string, BaselineEntry> _entries = new(ServerPath.Comparer);

    public int Count => _entries.Count;

    public BaselineEntry? Get(string serverPath) => _entries.GetValueOrDefault(serverPath);

    public void Set(BaselineEntry entry) => _entries[entry.ServerPath] = entry;

    public bool Remove(string serverPath) => _entries.Remove(serverPath);

    public IEnumerable<BaselineEntry> EntriesUnder(string scope) =>
        _entries.Values.Where(entry => ServerPath.IsSameOrUnder(entry.ServerPath, scope));

    public static Baseline Load(string file)
    {
        var baseline = new Baseline();
        if (!File.Exists(file))
        {
            return baseline;
        }

        foreach (var line in File.ReadLines(file))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var parts = line.Split('\t');
            if (parts.Length != 6)
            {
                throw new TfsEdException($"Workspace metadata '{file}' is corrupt.");
            }

            baseline.Set(new BaselineEntry(
                parts[5],
                parts[0] == "D",
                int.Parse(parts[1], CultureInfo.InvariantCulture),
                long.Parse(parts[2], CultureInfo.InvariantCulture),
                parts[3].Length == 0 ? null : parts[3],
                long.Parse(parts[4], CultureInfo.InvariantCulture)));
        }

        return baseline;
    }

    public void Save(string file)
    {
        var temp = file + ".tmp";
        using (var writer = new StreamWriter(temp, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            writer.NewLine = "\n";
            writer.WriteLine(Header);
            foreach (var entry in _entries.Values.OrderBy(entry => entry.ServerPath, ServerPath.Comparer))
            {
                writer.WriteLine(string.Join('\t',
                    entry.IsFolder ? "D" : "F",
                    entry.Version.ToString(CultureInfo.InvariantCulture),
                    entry.Size.ToString(CultureInfo.InvariantCulture),
                    entry.Hash ?? string.Empty,
                    entry.LastWriteTicks.ToString(CultureInfo.InvariantCulture),
                    entry.ServerPath));
            }
        }

        File.Move(temp, file, overwrite: true);
    }
}
