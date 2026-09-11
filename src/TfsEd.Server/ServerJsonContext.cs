using System.Text.Json.Serialization;

namespace TfsEd.Server;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ConnectionData))]
[JsonSerializable(typeof(ListResponse<TeamProject>))]
[JsonSerializable(typeof(ListResponse<TfvcItemDto>))]
[JsonSerializable(typeof(ListResponse<ChangesetDto>))]
[JsonSerializable(typeof(ListResponse<ChangeDto>))]
[JsonSerializable(typeof(ChangesetDto))]
[JsonSerializable(typeof(ItemBatchRequest))]
[JsonSerializable(typeof(ServerError))]
internal sealed partial class ServerJsonContext : JsonSerializerContext;
