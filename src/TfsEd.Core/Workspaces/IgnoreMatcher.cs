using System.Text.RegularExpressions;

namespace TfsEd.Core.Workspaces;

/// <summary>
/// Evaluates <c>.tfignore</c> files: one pattern per line, <c>#</c> comments, <c>!</c> negation,
/// <c>*</c>/<c>?</c> wildcards. Patterns without a path separator match an item name anywhere below the
/// <c>.tfignore</c> directory; patterns with a separator (or a leading <c>\</c>) are relative to it.
/// The last matching rule wins; an ignored directory ignores its whole subtree.
/// </summary>
public sealed class IgnoreMatcher
{
    public const string FileName = ".tfignore";

    public static readonly IgnoreMatcher Empty = new([]);

    private readonly IReadOnlyList<IgnoreRule> _rules;

    private IgnoreMatcher(IReadOnlyList<IgnoreRule> rules) => _rules = rules;

    /// <summary>Collects the rules of all directories from the workspace root down to <paramref name="directory"/>.</summary>
    public static IgnoreMatcher ForPath(string workspaceRoot, string directory)
    {
        var matcher = Empty.ForDirectory(workspaceRoot, string.Empty);
        var relative = Path.GetRelativePath(workspaceRoot, directory);
        if (relative == ".")
        {
            return matcher;
        }

        var current = workspaceRoot;
        var relativeDirectory = string.Empty;
        foreach (var segment in relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            relativeDirectory = relativeDirectory.Length == 0 ? segment : $"{relativeDirectory}/{segment}";
            matcher = matcher.ForDirectory(current, relativeDirectory);
        }

        return matcher;
    }

    /// <summary>Adds the rules of <c>.tfignore</c> in <paramref name="directory"/>, if present.</summary>
    public IgnoreMatcher ForDirectory(string directory, string relativeDirectory)
    {
        var file = Path.Combine(directory, FileName);
        return File.Exists(file) ? WithRules(File.ReadAllLines(file), relativeDirectory) : this;
    }

    public IgnoreMatcher WithRules(IEnumerable<string> lines, string relativeDirectory)
    {
        var rules = new List<IgnoreRule>(_rules);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            rules.Add(IgnoreRule.Parse(line, relativeDirectory));
        }

        return new IgnoreMatcher(rules);
    }

    /// <param name="relativePath">'/'-separated path relative to the workspace root.</param>
    public bool IsIgnored(string relativePath)
    {
        var ignored = false;
        foreach (var rule in _rules)
        {
            if (rule.Matches(relativePath))
            {
                ignored = !rule.Negate;
            }
        }

        return ignored;
    }

    private sealed class IgnoreRule
    {
        private readonly Regex _regex;
        private readonly string _baseDirectory;
        private readonly bool _anchored;

        private IgnoreRule(Regex regex, string baseDirectory, bool anchored, bool negate)
        {
            _regex = regex;
            _baseDirectory = baseDirectory;
            _anchored = anchored;
            Negate = negate;
        }

        public bool Negate { get; }

        public static IgnoreRule Parse(string line, string baseDirectory)
        {
            var negate = line[0] == '!';
            var pattern = (negate ? line[1..] : line).Replace('\\', '/').TrimEnd('/');
            var anchored = pattern.Contains('/');
            pattern = pattern.TrimStart('/');
            var regex = "^" + Regex.Escape(pattern).Replace(@"\*", "[^/]*").Replace(@"\?", "[^/]") + "$";
            return new IgnoreRule(new Regex(regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), baseDirectory, anchored, negate);
        }

        public bool Matches(string relativePath)
        {
            var candidate = relativePath;
            if (_baseDirectory.Length > 0)
            {
                if (!relativePath.StartsWith(_baseDirectory + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                candidate = relativePath[(_baseDirectory.Length + 1)..];
            }

            if (!_anchored)
            {
                candidate = candidate[(candidate.LastIndexOf('/') + 1)..];
            }

            return _regex.IsMatch(candidate);
        }
    }
}
