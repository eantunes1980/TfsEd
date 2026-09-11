using System.Text.Json.Serialization;
using TfsEd.Core.Configuration;

namespace TfsEd.Core;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class CoreJsonContext : JsonSerializerContext;
