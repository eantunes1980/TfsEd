using System.Text.Json.Serialization;

namespace TfsEd.Server;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ConnectionData))]
[JsonSerializable(typeof(ListResponse<TeamProject>))]
[JsonSerializable(typeof(ServerError))]
internal sealed partial class ServerJsonContext : JsonSerializerContext;
