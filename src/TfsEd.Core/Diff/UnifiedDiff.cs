using System.Text;

namespace TfsEd.Core.Diff;

/// <summary>Produces unified diffs (as used by git and patch) for text files.</summary>
public static class UnifiedDiff
{
    private const int BinaryProbeLength = 8000;

    /// <returns>The diff, or an empty string if the contents are equal line by line.</returns>
    public static string Format(string oldLabel, string newLabel, byte[] oldContent, byte[] newContent, int context = 3)
    {
        if (IsBinary(oldContent) || IsBinary(newContent))
        {
            return oldContent.AsSpan().SequenceEqual(newContent) ? string.Empty : $"Binary files {oldLabel} and {newLabel} differ\n";
        }

        var oldLines = SplitLines(Decode(oldContent));
        var newLines = SplitLines(Decode(newContent));
        var edits = MyersDiff.Compute(oldLines, newLines);
        if (edits.All(edit => edit.Kind == EditKind.Equal))
        {
            return oldContent.AsSpan().SequenceEqual(newContent)
                ? string.Empty
                : $"Files {oldLabel} and {newLabel} differ only in line endings or encoding\n";
        }

        var output = new StringBuilder();
        output.Append("--- ").Append(oldLabel).Append('\n');
        output.Append("+++ ").Append(newLabel).Append('\n');
        foreach (var (start, end) in GroupHunks(edits, context))
        {
            AppendHunk(output, edits, start, end, oldLines, newLines);
        }

        return output.ToString();
    }

    public static bool IsBinary(byte[] content)
    {
        // UTF-16 text contains zero bytes but starts with a byte order mark.
        if (content.Length >= 2 && ((content[0] == 0xFF && content[1] == 0xFE) || (content[0] == 0xFE && content[1] == 0xFF)))
        {
            return false;
        }

        return content.AsSpan(0, Math.Min(content.Length, BinaryProbeLength)).Contains((byte)0);
    }

    private static string Decode(byte[] content)
    {
        using var reader = new StreamReader(new MemoryStream(content), new UTF8Encoding(false, throwOnInvalidBytes: true), detectEncodingFromByteOrderMarks: true);
        try
        {
            return reader.ReadToEnd();
        }
        catch (DecoderFallbackException)
        {
            // Legacy ANSI files; Latin-1 keeps every byte displayable.
            return Encoding.Latin1.GetString(content);
        }
    }

    private static List<string> SplitLines(string text)
    {
        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    private static IEnumerable<(int Start, int End)> GroupHunks(List<Edit> edits, int context)
    {
        var changeIndexes = Enumerable.Range(0, edits.Count).Where(i => edits[i].Kind != EditKind.Equal).ToList();
        var start = changeIndexes[0];
        var end = changeIndexes[0];
        foreach (var index in changeIndexes.Skip(1))
        {
            if (index - end > 2 * context)
            {
                yield return (Math.Max(0, start - context), Math.Min(edits.Count - 1, end + context));
                start = index;
            }

            end = index;
        }

        yield return (Math.Max(0, start - context), Math.Min(edits.Count - 1, end + context));
    }

    private static void AppendHunk(StringBuilder output, List<Edit> edits, int start, int end, List<string> oldLines, List<string> newLines)
    {
        var oldCount = 0;
        var newCount = 0;
        for (var i = start; i <= end; i++)
        {
            if (edits[i].Kind != EditKind.Insert)
            {
                oldCount++;
            }

            if (edits[i].Kind != EditKind.Delete)
            {
                newCount++;
            }
        }

        var first = edits[start];
        var oldStart = oldCount == 0 ? first.OldIndex : first.OldIndex + 1;
        var newStart = newCount == 0 ? first.NewIndex : first.NewIndex + 1;
        output.Append($"@@ -{oldStart},{oldCount} +{newStart},{newCount} @@\n");

        for (var i = start; i <= end; i++)
        {
            var edit = edits[i];
            var line = edit.Kind switch
            {
                EditKind.Equal => " " + oldLines[edit.OldIndex],
                EditKind.Delete => "-" + oldLines[edit.OldIndex],
                _ => "+" + newLines[edit.NewIndex],
            };
            output.Append(line).Append('\n');
        }
    }
}
