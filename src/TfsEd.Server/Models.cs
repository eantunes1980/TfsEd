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
