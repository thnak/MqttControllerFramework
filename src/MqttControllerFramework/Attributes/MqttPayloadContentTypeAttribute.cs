namespace MqttControllerFramework.Attributes;

/// <summary>
///     Declares the MQTT content-type string for a custom payload parser type.
///     Apply on a class that implements <see cref="Abstracts.IMqttPayloadParser{T}"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class MqttPayloadContentTypeAttribute(string contentType) : Attribute
{
    /// <summary>Content-type string that triggers this parser.</summary>
    public string ContentType { get; } = contentType;
}
