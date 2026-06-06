using System.Security.Cryptography.X509Certificates;

namespace MqttControllerFramework.Security;

/// <summary>
///     Extends <c>ICertificateProvider</c> with runtime certificate management:
///     hot-swapping the server TLS certificate and managing the client certificate allowlist.
/// </summary>
public interface IHotSwappableServerCertProvider
{
    /// <summary>Adds a client certificate to the allowlist and persists it to disk.</summary>
    void InstallNewClientCert(X509Certificate2 certificate);

    /// <summary>Returns the thumbprints of all trusted client certificates.</summary>
    List<string> GetInstalledClientCertThumbprints();

    /// <summary>Returns the subjects of all trusted client certificates.</summary>
    List<string> GetInstalledClientCerts();

    /// <summary>Removes a client certificate from the allowlist by thumbprint.</summary>
    void RemoveClientCert(string thumbprint);
}
