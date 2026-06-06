namespace MqttControllerFramework.Events;

/// <summary>
///     Register implementations of this interface to be notified when a client unsubscribes from a topic.
/// </summary>
public interface IMqttClientUnsubscribedTopicEvent
{
    /// <summary>Called after a client successfully unsubscribes from a topic.</summary>
    Task OnClientUnsubscribedTopicAsync(string clientId, string topicFilter);
}
