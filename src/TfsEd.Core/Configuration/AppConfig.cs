namespace TfsEd.Core.Configuration;

/// <summary>Persistent per-user settings (never contains secrets).</summary>
public sealed class AppConfig
{
    /// <summary>Collection used when no <c>--collection</c> option is given.</summary>
    public string? DefaultCollection { get; set; }

    /// <summary>Optional root CA certificate (PEM or DER) trusted in addition to the system store.</summary>
    public string? CaCertificatePath { get; set; }
}
