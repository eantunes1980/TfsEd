using System.Text.Json;

namespace TfsEd.Core.Configuration;

/// <summary>Loads and saves <see cref="AppConfig"/> as JSON.</summary>
public sealed class ConfigStore
{
    private readonly string _path;

    public ConfigStore(string path) => _path = path;

    public AppConfig Load()
    {
        if (!File.Exists(_path))
        {
            return new AppConfig();
        }

        try
        {
            using var stream = File.OpenRead(_path);
            return JsonSerializer.Deserialize(stream, CoreJsonContext.Default.AppConfig) ?? new AppConfig();
        }
        catch (JsonException ex)
        {
            throw new TfsEdException($"Configuration file '{_path}' is corrupt: {ex.Message}", ex);
        }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(config, CoreJsonContext.Default.AppConfig));
        File.Move(temp, _path, overwrite: true);
    }
}
