using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using TfsEd.Core;

namespace TfsEd.Server;

public static class ServerHttpClientFactory
{
    /// <summary>
    /// Creates an <see cref="HttpClient"/> that validates TLS against the system store and, optionally,
    /// an additional root CA (for servers with an internal certificate authority). Validation is never disabled.
    /// </summary>
    public static HttpClient Create(string? caCertificatePath)
    {
        var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
        if (!string.IsNullOrEmpty(caCertificatePath))
        {
            var root = LoadCertificate(caCertificatePath);
            handler.SslOptions.RemoteCertificateValidationCallback =
                (_, certificate, _, errors) => IsTrusted(certificate, errors, root);
        }

        var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"TfsEd/{typeof(ServerHttpClientFactory).Assembly.GetName().Version?.ToString(3)}");
        return client;
    }

    internal static bool IsTrusted(X509Certificate? certificate, SslPolicyErrors errors, X509Certificate2 root)
    {
        if (errors == SslPolicyErrors.None)
        {
            return true;
        }

        // Only an untrusted chain may be rescued by the custom root; name mismatches are always fatal.
        if (certificate is null || errors != SslPolicyErrors.RemoteCertificateChainErrors)
        {
            return false;
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        using var leaf = new X509Certificate2(certificate);
        return chain.Build(leaf);
    }

    private static X509Certificate2 LoadCertificate(string path)
    {
        try
        {
            return X509CertificateLoader.LoadCertificateFromFile(path);
        }
        catch (Exception ex) when (ex is IOException or System.Security.Cryptography.CryptographicException)
        {
            throw new TfsEdException($"Could not load CA certificate '{path}': {ex.Message}", ex);
        }
    }
}
