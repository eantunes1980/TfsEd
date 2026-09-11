namespace TfsEd.Core.Diff;

public enum EditKind
{
    Equal,
    Delete,
    Insert,
}

/// <summary>
/// One step of an edit script. For <see cref="EditKind.Insert"/>, <see cref="OldIndex"/> is the number of old
/// lines consumed so far; for <see cref="EditKind.Delete"/>, <see cref="NewIndex"/> is the number of new lines consumed.
/// </summary>
public readonly record struct Edit(EditKind Kind, int OldIndex, int NewIndex);

/// <summary>Eugene Myers' O((N+M)D) shortest edit script algorithm.</summary>
public static class MyersDiff
{
    public static List<Edit> Compute(IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines)
    {
        int n = oldLines.Count, m = newLines.Count, max = n + m;
        var offset = max + 1;
        var v = new int[2 * max + 3];

        // trace[d] holds v for k in [-(d+1), d+1] before round d.
        var trace = new List<int[]>();
        for (var d = 0; d <= max; d++)
        {
            trace.Add(v.AsSpan(offset - d - 1, 2 * d + 3).ToArray());
            for (var k = -d; k <= d; k += 2)
            {
                var x = k == -d || (k != d && v[offset + k - 1] < v[offset + k + 1])
                    ? v[offset + k + 1]
                    : v[offset + k - 1] + 1;
                var y = x - k;
                while (x < n && y < m && string.Equals(oldLines[x], newLines[y], StringComparison.Ordinal))
                {
                    x++;
                    y++;
                }

                v[offset + k] = x;
                if (x >= n && y >= m)
                {
                    return Backtrack(trace, n, m);
                }
            }
        }

        throw new InvalidOperationException("Unreachable: an edit script always exists.");
    }

    private static List<Edit> Backtrack(List<int[]> trace, int n, int m)
    {
        var edits = new List<Edit>();
        int x = n, y = m;
        for (var d = trace.Count - 1; d >= 0; d--)
        {
            var v = trace[d];
            int Get(int k) => v[k + d + 1];

            var k = x - y;
            var previousK = k == -d || (k != d && Get(k - 1) < Get(k + 1)) ? k + 1 : k - 1;
            var previousX = Get(previousK);
            var previousY = previousX - previousK;

            while (x > previousX && y > previousY)
            {
                edits.Add(new Edit(EditKind.Equal, x - 1, y - 1));
                x--;
                y--;
            }

            if (d > 0)
            {
                edits.Add(x == previousX
                    ? new Edit(EditKind.Insert, x, y - 1)
                    : new Edit(EditKind.Delete, x - 1, y));
            }

            x = previousX;
            y = previousY;
        }

        edits.Reverse();
        return edits;
    }
}
