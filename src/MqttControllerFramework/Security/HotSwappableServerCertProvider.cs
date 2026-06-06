using System.Collections.Concurrent;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using MQTTnet.Certificates;
using MqttControllerFramework.Configuration;

namespace MqttControllerFramework.Security;

/// <summary>
///     TLS certificate provider that supports runtime hot-swap of the server certificate
///     and an in-memory allowlist of trusted client certificates.
/// </summary>
public sealed class HotSwappableServerCertProvider : ICertificateProvider, IDisposable, IHotSwappableServerCertProvider
{
    private readonly IOptions<MqttServerSettings> _options;
    private X509Certificate2? _currentCertificate;
    private readonly ConcurrentBag<X509Certificate2> _clientCerts = new();

    /// <summary>Loads the server certificate and any persisted client certificates on startup.</summary>
    public HotSwappableServerCertProvider(IOptions<MqttServerSettings> options)
    {
        _options = options;
        _currentCertificate = LoadBrokerCert();

        var storage = options.Value.ClientCertStorage;
        if (!Directory.Exists(storage)) return;
        foreach (var file in Directory.GetFiles(storage, "*.cer"))
            InstallNewClientCert(LoadCertFromFile(file));
    }

    private X509Certificate2 LoadBrokerCert()
    {
        var settings = _options.Value;
        if (!string.IsNullOrEmpty(settings.PkcsKeyPath))
        {
            var publicCert = LoadCertFromFile(settings.PkcsPath);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(settings.PkcsKeyPath));
            return publicCert.CopyWithPrivateKey(rsa);
        }
        return LoadPkcs12FromFile(settings.PkcsPath, settings.PkcsPassword);
    }

    /// <inheritdoc/>
    public X509Certificate2 GetCertificate() =>
        Interlocked.CompareExchange(ref _currentCertificate, null, null)!;

    /// <summary>Atomically replaces the server certificate used during TLS handshakes.</summary>
    public void UpdateCertificate(X509Certificate2 newCertificate)
    {
        ArgumentNullException.ThrowIfNull(newCertificate);
        var previous = Interlocked.Exchange(ref _currentCertificate, newCertificate);
        try { previous?.Dispose(); } catch { /* best-effort */ }
    }

    /// <inheritdoc/>
    public void InstallNewClientCert(X509Certificate2 certificate)
    {
        _clientCerts.Add(certificate);
        var storage = _options.Value.ClientCertStorage;
        if (!string.IsNullOrEmpty(storage))
        {
            Directory.CreateDirectory(storage);
            File.WriteAllBytes(Path.Combine(storage, $"{certificate.Thumbprint}.cer"), certificate.Export(X509ContentType.Cert));
        }
    }

    /// <inheritdoc/>
    public List<string> GetInstalledClientCertThumbprints() => _clientCerts.Select(c => c.Thumbprint).ToList();

    /// <inheritdoc/>
    public List<string> GetInstalledClientCerts() => _clientCerts.Select(c => c.Subject).ToList();

    /// <inheritdoc/>
    public void RemoveClientCert(string thumbprint)
    {
        var cert = _clientCerts.FirstOrDefault(c => c.Thumbprint == thumbprint);
        if (cert == null) return;
        _clientCerts.TryTake(out _);
        var storage = _options.Value.ClientCertStorage;
        if (!string.IsNullOrEmpty(storage))
        {
            var filePath = Path.Combine(storage, $"{thumbprint}.cer");
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    /// <summary>Validates a client certificate against the allowlist.</summary>
    public bool RemoteCertificateValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        if (certificate == null && sslPolicyErrors == SslPolicyErrors.RemoteCertificateNotAvailable) return true;
        var thumbprint = (certificate as X509Certificate2)?.Thumbprint;
        return _clientCerts.Any(c => c.Thumbprint == thumbprint);
    }

    // ── Compat helpers ────────────────────────────────────────────────────
    // X509CertificateLoader was added in .NET 9; fall back to the X509Certificate2 ctor on net8.

    private static X509Certificate2 LoadCertFromFile(string path)
    {
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadCertificateFromFile(path);
#else
#pragma warning disable SYSLIB0057
        return new X509Certificate2(path);
#pragma warning restore SYSLIB0057
#endif
    }

    private static X509Certificate2 LoadPkcs12FromFile(string path, string? password)
    {
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12FromFile(path, password);
#else
#pragma warning disable SYSLIB0057
        return new X509Certificate2(path, password);
#pragma warning restore SYSLIB0057
#endif
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        try { _currentCertificate?.Dispose(); } catch { /* swallow */ }
        while (_clientCerts.TryTake(out var c))
            try { c.Dispose(); } catch { /* ignore */ }
    }
}
