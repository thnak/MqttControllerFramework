using MQTTnet.Server;

namespace MqttControllerFramework.Stats;

/// <summary>Tracks real-time statistics for the MQTT broker.</summary>
public interface IMqttBrokerStatsService
{
    /// <summary>Updates statistics when a client connects.</summary>
    Task MqttServerOnClientConnectedAsync(ClientConnectedEventArgs args);
    /// <summary>Updates statistics when a client disconnects.</summary>
    Task MqttServerOnClientDisconnectedAsync(ClientDisconnectedEventArgs args);
    /// <summary>Updates subscription statistics when a client subscribes.</summary>
    Task MqttServerOnInterceptingSubscriptionAsync(InterceptingSubscriptionEventArgs args);
    /// <summary>Updates subscription statistics when a client unsubscribes.</summary>
    Task MqttServerOnInterceptingUnsubscriptionAsync(InterceptingUnsubscriptionEventArgs args);
    /// <summary>Updates message and byte statistics when a publish is intercepted.</summary>
    Task MqttServerOnInterceptingPublishAsync(InterceptingPublishEventArgs args, bool isServer);
    /// <summary>Updates statistics during connection validation.</summary>
    Task MqttServerOnValidatingConnectionAsync(ValidatingConnectionEventArgs args);
    /// <summary>Updates statistics when a session is deleted.</summary>
    Task MqttServerOnSessionDeletedAsync(SessionDeletedEventArgs args);

    /// <summary>Returns the total number of messages processed since startup.</summary>
    ulong GetTotalMessageCount();
    /// <summary>Returns the cumulative payload size in bytes of all processed messages.</summary>
    ulong GetTotalMessageSize();
    /// <summary>Returns the current number of connected clients.</summary>
    ulong GetTotalConnectedClientCount();
    /// <summary>Returns the number of currently retained messages.</summary>
    ulong GetRetainedMessageCount();
    /// <summary>Returns the number of active sessions.</summary>
    int GetSessionCount();
    /// <summary>Returns the total number of active subscriptions across all clients.</summary>
    int GetSubscriptionCount();
    /// <summary>Returns per-topic statistics keyed by topic string.</summary>
    Dictionary<string, TopicStatistics> GetTopicSummary();
    /// <summary>Returns whether the broker is currently accepting new connections.</summary>
    bool GetAcceptNewConnections();
    /// <summary>Sets whether the broker accepts new connections.</summary>
    void SetAcceptNewConnections(bool accept);
    /// <summary>Returns the elapsed time since the broker started.</summary>
    TimeSpan GetUptime();
    /// <summary>Returns the total bytes received by the broker from all clients.</summary>
    ulong GetTotalBytesReceivedByBroker();
    /// <summary>Returns the total bytes sent by the broker to all clients.</summary>
    ulong GetTotalBytesSentByBroker();
    /// <summary>Returns the total bytes received by clients (broker perspective).</summary>
    ulong GetTotalBytesReceivedByClients();
    /// <summary>Returns the total bytes sent by clients (broker perspective).</summary>
    ulong GetTotalBytesSentByClients();
    /// <summary>Records the broker start time for uptime calculation.</summary>
    void MarkBrokerStarted();
    /// <summary>Increments the count of messages dropped due to capacity or filtering.</summary>
    void IncrementDroppedMessageCount();
    /// <summary>Returns the total number of dropped messages since startup.</summary>
    ulong GetDroppedMessageCount();
    /// <summary>Increments the count of messages published but not consumed by any subscriber.</summary>
    void IncrementUnconsumedMessageCount();
    /// <summary>Returns the total number of unconsumed messages since startup.</summary>
    ulong GetUnconsumedMessageCount();
    /// <summary>Increments the count of messages successfully acknowledged by subscribers.</summary>
    void IncrementAcknowledgedMessageCount();
    /// <summary>Returns the total number of acknowledged messages since startup.</summary>
    ulong GetAcknowledgedMessageCount();
    /// <summary>Increments the count of messages overwritten in the retain queue.</summary>
    void IncrementQueueOverwriteCount();
    /// <summary>Returns the total number of queue overwrites since startup.</summary>
    ulong GetQueueOverwriteCount();
}
