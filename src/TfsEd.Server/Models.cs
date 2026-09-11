namespace TfsEd.Server;

/// <summary>Response of <c>_apis/connectionData</c>.</summary>
public sealed class ConnectionData
{
    public Identity? AuthenticatedUser { get; set; }

    public Guid InstanceId { get; set; }

    public string? DeploymentType { get; set; }
}

public sealed class Identity
{
    public string? Id { get; set; }

    public string? ProviderDisplayName { get; set; }
}

public sealed class TeamProject
{
    public string Name { get; set; } = string.Empty;

    public string? State { get; set; }
}

/// <summary>Standard REST envelope for collections: <c>{ "count": n, "value": [...] }</c>.</summary>
public sealed class ListResponse<T>
{
    public int Count { get; set; }

    public List<T> Value { get; set; } = [];
}

internal sealed class ServerError
{
    public string? Message { get; set; }
}

internal sealed class TfvcItemDto
{
    public string Path { get; set; } = string.Empty;

    public int Version { get; set; }

    public bool IsFolder { get; set; }

    public long Size { get; set; }

    public string? HashValue { get; set; }

    public DateTimeOffset ChangeDate { get; set; }
}

internal sealed class IdentityRefDto
{
    public string? DisplayName { get; set; }

    public string? UniqueName { get; set; }
}

internal sealed class ChangesetDto
{
    public int ChangesetId { get; set; }

    public IdentityRefDto? Author { get; set; }

    public DateTimeOffset CreatedDate { get; set; }

    public string? Comment { get; set; }
}

internal sealed class ChangeDto
{
    public TfvcItemDto? Item { get; set; }

    public string? ChangeType { get; set; }
}

internal sealed class ItemBatchRequest
{
    public List<ItemDescriptorDto> ItemDescriptors { get; set; } = [];
}

internal sealed class ItemDescriptorDto
{
    public string Path { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string VersionType { get; set; } = "changeset";

    public string RecursionLevel { get; set; } = "none";
}
