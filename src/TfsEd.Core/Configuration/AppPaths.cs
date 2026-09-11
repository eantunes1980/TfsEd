namespace TfsEd.Core.Configuration;

/// <summary>Locations of per-user TfsEd files.</summary>
public sealed class AppPaths
{
    public AppPaths(string configDirectory) => ConfigDirectory = configDirectory;

    public string ConfigDirectory { get; }

    public string ConfigFile => Path.Combine(ConfigDirectory, "config.json");

    /// <summary>Fallback credential file, used only when no OS keychain is available.</summary>
    public string CredentialsFile => Path.Combine(ConfigDirectory, "credentials.json");

    /// <summary>
    /// Resolves the config directory: <c>TFSED_CONFIG_DIR</c>, then <c>%APPDATA%\tfsed</c> on Windows,
    /// otherwise <c>$XDG_CONFIG_HOME/tfsed</c> or <c>~/.config/tfsed</c>.
    /// </summary>
    public static AppPaths Default()
    {
        var overridden = Environment.GetEnvironmentVariable("TFSED_CONFIG_DIR");
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            return new AppPaths(overridden);
        }

        if (OperatingSystem.IsWindows())
        {
            return new AppPaths(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "tfsed"));
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var baseDir = string.IsNullOrWhiteSpace(xdg)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : xdg;
        return new AppPaths(Path.Combine(baseDir, "tfsed"));
    }
}
