namespace MqttControllerFramework.Abstracts;

/// <summary>
///     Parser for MQTT binary payloads. Implement this on a type decorated with
///     <see cref="Attributes.MqttPayloadContentTypeAttribute"/> to enable custom deserialization.
/// </summary>
public interface IMqttPayloadParser<out T>
{
    /// <summary>Parses the raw MQTT payload bytes into <typeparamref name="T"/>.</summary>
    T Parse(ReadOnlySpan<byte> payload);
}
