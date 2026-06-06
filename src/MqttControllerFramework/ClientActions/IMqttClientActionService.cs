using System.Buffers;

namespace MqttControllerFramework.ClientActions;

/// <summary>
///     Publishes messages from the broker to MQTT topics.
///     Server-originated messages are automatically tagged so the broker ignores its own traffic.
/// </summary>
public interface IMqttClientActionService
{
    /// <summary>Publishes a raw payload to a topic.</summary>
    Task SendMessageAsync(string topic, ReadOnlySequence<byte> message, CancellationToken cancellationToken = default);

    /// <summary>Publishes a payload and waits for a typed response on the reply topic.</summary>
    Task<TResult?> SendMessageAsync<TResult>(string topic, ReadOnlySequence<byte> message, CancellationToken cancellationToken = default);

    /// <summary>Sends an empty request to a topic and waits for a typed response.</summary>
    Task<TResult?> GetDataFromTopicAsync<TResult>(string topic, CancellationToken cancellationToken = default);
}
