using MQTTnet.Server;

namespace MqttControllerFramework.Stats;

/// <summary>Tracks real-time statistics for the MQTT broker.</summary>
public interface IMqttBrokerStatsService
{
    Task MqttServerOnClientConnectedAsync(ClientConnectedEventArgs args);
    Task MqttServerOnClientDisconnectedAsync(ClientDisconnectedEventArgs args);
    Task MqttServerOnInterceptingSubscriptionAsync(InterceptingSubscriptionEventArgs args);
    Task MqttServerOnInterceptingUnsubscriptionAsync(InterceptingUnsubscriptionEventArgs args);
    Task MqttServerOnInterceptingPublishAsync(InterceptingPublishEventArgs args, bool isServer);
    Task MqttServerOnValidatingConnectionAsync(ValidatingConnectionEventArgs args);
    Task MqttServerOnSessionDeletedAsync(SessionDeletedEventArgs args);

    ulong GetTotalMessageCount();
    ulong GetTotalMessageSize();
    ulong GetTotalConnectedClientCount();
    ulong GetRetainedMessageCount();
    int GetSessionCount();
    int GetSubscriptionCount();
    Dictionary<string, TopicStatistics> GetTopicSummary();
    bool GetAcceptNewConnections();
    void SetAcceptNewConnections(bool accept);
    TimeSpan GetUptime();
    ulong GetTotalBytesReceivedByBroker();
    ulong GetTotalBytesSentByBroker();
    ulong GetTotalBytesReceivedByClients();
    ulong GetTotalBytesSentByClients();
    void MarkBrokerStarted();
    void IncrementDroppedMessageCount();
    ulong GetDroppedMessageCount();
    void IncrementUnconsumedMessageCount();
    ulong GetUnconsumedMessageCount();
    void IncrementAcknowledgedMessageCount();
    ulong GetAcknowledgedMessageCount();
    void IncrementQueueOverwriteCount();
    ulong GetQueueOverwriteCount();
}
