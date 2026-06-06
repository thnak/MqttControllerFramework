namespace MqttControllerFramework.Events;

/// <summary>
///     Register implementations of this interface to be notified when a client subscribes to a topic.
/// </summary>
public interface IMqttClientSubscribedTopicEvent
{
    /// <summary>Called after a client successfully subscribes to a topic.</summary>
    Task OnClientSubscribedTopicAsync(string clientId, string topic);
}
