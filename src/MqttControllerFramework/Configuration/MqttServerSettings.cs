namespace MqttControllerFramework.Configuration;

/// <summary>Configuration for the MQTT broker server.</summary>
public sealed class MqttServerSettings
{
    /// <summary>Default configuration section name.</summary>
    public const string SectionName = "MqttSettings";

    /// <summary>Listen on plain TCP (default: true).</summary>
    public bool EnableNonSsl { get; set; } = true;

    /// <summary>Listen on TLS (default: false).</summary>
    public bool EnableSsl { get; set; }

    /// <summary>Plain-TCP port (default: 1883).</summary>
    public int NonSslPort { get; set; } = 1883;

    /// <summary>TLS port (default: 8883).</summary>
    public int SslPort { get; set; } = 8883;

    /// <summary>Path to PKCS#12 (.pfx) or PEM certificate (.crt) file for TLS.</summary>
    public string PkcsPath { get; set; } = string.Empty;

    /// <summary>Password for the PKCS#12 bundle. Leave empty for PEM.</summary>
    public string PkcsPassword { get; set; } = string.Empty;

    /// <summary>
    ///     Path to PEM private-key file (.key). When non-empty,
    ///     <see cref="PkcsPath"/> is the certificate (.crt) and this is the key.
    ///     Leave empty to use the standard PKCS#12 bundle.
    /// </summary>
    public string PkcsKeyPath { get; set; } = string.Empty;

    /// <summary>Directory where trusted client certificates (.cer) are persisted.</summary>
    public string ClientCertStorage { get; set; } = string.Empty;

    /// <summary>
    ///     File path where retained messages are persisted across restarts
    ///     (default: <c>RetainedMessages.json</c> in the working directory).
    /// </summary>
    public string RetainedMessagesFilePath { get; set; } = "RetainedMessages.json";

    /// <summary>
    ///     User-property name used to tag server-originated messages
    ///     so that the broker ignores its own traffic (default: <c>x-mqtt-origin</c>).
    /// </summary>
    public string ServerOriginPropertyName { get; set; } = "x-mqtt-origin";

    /// <summary>
    ///     Value of <see cref="ServerOriginPropertyName"/> that marks a server-originated message
    ///     (default: <c>server</c>).
    /// </summary>
    public string ServerOriginPropertyValue { get; set; } = "server";
}
