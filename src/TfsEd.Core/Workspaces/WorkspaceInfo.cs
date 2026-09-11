namespace TfsEd.Core.Workspaces;

/// <summary>Contents of <c>.tf/workspace.json</c>.</summary>
public sealed class WorkspaceInfo
{
    public string Collection { get; set; } = string.Empty;

    /// <summary>Server folder mapped to the workspace root.</summary>
    public string ServerPath { get; set; } = string.Empty;

    /// <summary>Changeset of the last full get of the workspace root (0 = never).</summary>
    public int Version { get; set; }
}
