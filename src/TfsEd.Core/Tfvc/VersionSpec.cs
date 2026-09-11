using System.Globalization;

namespace TfsEd.Core.Tfvc;

/// <summary>A TFVC version: the latest version ("T") or a changeset ("C123").</summary>
public readonly record struct VersionSpec(int? Changeset)
{
    public static VersionSpec Latest => new(null);

    public bool IsLatest => Changeset is null;

    public static VersionSpec Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim() is "T" or "t")
        {
            return Latest;
        }

        var value = text.Trim();
        if (value[0] is 'C' or 'c')
        {
            value = value[1..];
        }

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var changeset) && changeset > 0
            ? new VersionSpec(changeset)
            : throw new TfsEdException($"Unsupported version '{text}'. Use T (latest) or C<changeset>, e.g. C1234.");
    }

    public override string ToString() => IsLatest ? "T" : $"C{Changeset}";
}
