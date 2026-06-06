# TLS and Security

## Overview

MqttControllerFramework supports TLS through `HotSwappableServerCertProvider`. It loads a server certificate at startup and allows you to swap it at runtime without restarting the broker. It also maintains an in-memory allowlist of trusted client certificates.

---

## Enabling TLS

Set `EnableSsl: true` in `MqttSettings` and provide the certificate path.

### PKCS#12 / PFX bundle

```json
"MqttSettings": {
  "EnableNonSsl": false,
  "EnableSsl": true,
  "SslPort": 8883,
  "PkcsPath": "/etc/certs/broker.pfx",
  "PkcsPassword": "changeme"
}
```

### PEM certificate + separate private key

```json
"MqttSettings": {
  "EnableSsl": true,
  "SslPort": 8883,
  "PkcsPath": "/etc/certs/broker.crt",
  "PkcsKeyPath": "/etc/certs/broker.key"
}
```

When `PkcsKeyPath` is non-empty the framework treats `PkcsPath` as the public certificate and `PkcsKeyPath` as the RSA private-key PEM.

### Running both plain and TLS simultaneously

```json
"MqttSettings": {
  "EnableNonSsl": true,
  "NonSslPort": 1883,
  "EnableSsl": true,
  "SslPort": 8883,
  "PkcsPath": "/etc/certs/broker.pfx",
  "PkcsPassword": "changeme"
}
```

---

## Runtime Certificate Hot-Swap

Inject `IHotSwappableServerCertProvider` anywhere in your application and call `UpdateCertificate`:

```csharp
public class CertRotationJob(IHotSwappableServerCertProvider certProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(24), ct);

            var newCert = LoadLatestCertificate();
            certProvider.UpdateCertificate(newCert);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

The swap is atomic — new TLS handshakes immediately use the new certificate; existing connections are unaffected.

---

## Client Certificate Allowlist

`HotSwappableServerCertProvider` maintains an in-memory allowlist of trusted client certificates. Only clients presenting a certificate with a matching thumbprint are allowed.

```csharp
public class DeviceOnboardingService(IHotSwappableServerCertProvider certProvider)
{
    public void OnboardDevice(byte[] certBytes)
    {
        var cert = new X509Certificate2(certBytes);
        certProvider.InstallNewClientCert(cert);
    }

    public void RevokeDevice(string thumbprint)
    {
        certProvider.RemoveClientCert(thumbprint);
    }

    public List<string> ListDevices()
        => certProvider.GetInstalledClientCertThumbprints();
}
```

### Persisting client certificates

Set `ClientCertStorage` to a directory path. Installed certificates are saved as `.cer` files and reloaded automatically on startup:

```json
"MqttSettings": {
  "ClientCertStorage": "/var/mqtt/client-certs"
}
```

---

## `IHotSwappableServerCertProvider` API

| Method | Description |
|---|---|
| `UpdateCertificate(X509Certificate2)` | Atomically replaces the server certificate |
| `GetCertificate()` | Returns the current server certificate |
| `InstallNewClientCert(X509Certificate2)` | Adds a certificate to the client allowlist (persisted if `ClientCertStorage` is set) |
| `RemoveClientCert(string thumbprint)` | Removes a certificate from the allowlist |
| `GetInstalledClientCertThumbprints()` | Lists thumbprints of all installed client certificates |
| `GetInstalledClientCerts()` | Lists subjects of all installed client certificates |

---

## Notes

- `HotSwappableServerCertProvider` implements both `ICertificateProvider` (MQTTnet) and `IHotSwappableServerCertProvider`. It is registered as a singleton.
- On .NET 9+ the framework uses `X509CertificateLoader` (no SYSLIB0057); on .NET 8 it falls back to `X509Certificate2` constructors.
- The client certificate validation callback uses thumbprint matching — full chain validation is not performed by default.
