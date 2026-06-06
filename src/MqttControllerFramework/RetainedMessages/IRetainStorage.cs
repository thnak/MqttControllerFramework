using MQTTnet;

namespace MqttControllerFramework.RetainedMessages;

/// <summary>
///     Pluggable storage backend for retained MQTT messages.
///     Register a custom implementation via <c>builder.WithRetainStorage&lt;T&gt;()</c>
///     to persist retained messages in a database, Redis, blob storage, etc.
///     The built-in default is file-based JSON persistence.
/// </summary>
public interface IRetainStorage
{
    /// <summary>
    ///     Called on broker startup to load the initial set of retained messages.
    ///     Return an empty list if no messages have been persisted yet.
    /// </summary>
    Task<IReadOnlyList<MqttApplicationMessage>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Called whenever a retained message is added, updated, or removed.
    ///     <paramref name="messages"/> is the full current snapshot of all retained messages.
    /// </summary>
    Task SaveAsync(IReadOnlyList<MqttApplicationMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>Called when all retained messages are cleared by the broker.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
