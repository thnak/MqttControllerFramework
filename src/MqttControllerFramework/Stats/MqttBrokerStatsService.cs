using System.Collections.Concurrent;
using MQTTnet.Server;

namespace MqttControllerFramework.Stats;

/// <summary>Default in-memory implementation of <see cref="IMqttBrokerStatsService"/>.</summary>
public sealed class MqttBrokerStatsService : IMqttBrokerStatsService
{
    private readonly ConcurrentDictionary<string, bool> _mqttSessions = new();
    private readonly ConcurrentDictionary<string, int> _subscriptions = new();
    private readonly ConcurrentDictionary<string, TopicStatistics> _topicStats = new();
    private ulong _connectedClients;
    private ulong _retainedMessages;
    private ulong _totalBytesTransferred;
    private ulong _totalMessages;
    private bool _acceptNewConnections = true;
    private DateTime? _brokerStartTime;
    private ulong _bytesReceivedByBroker;
    private ulong _bytesSentByBroker;
    private ulong _bytesReceivedByClients;
    private ulong _bytesSentByClients;
    private ulong _droppedMessages;
    private ulong _unconsumedMessages;
    private ulong _acknowledgedMessages;
    private ulong _queueOverwrites;

    public Task MqttServerOnClientConnectedAsync(ClientConnectedEventArgs args)
    {
        Interlocked.Increment(ref _connectedClients);
        _mqttSessions.TryAdd(args.ClientId, false);
        return Task.CompletedTask;
    }

    public Task MqttServerOnClientDisconnectedAsync(ClientDisconnectedEventArgs args)
    {
        Interlocked.Decrement(ref _connectedClients);
        _mqttSessions.TryRemove(args.ClientId, out _);
        return Task.CompletedTask;
    }

    public Task MqttServerOnInterceptingSubscriptionAsync(InterceptingSubscriptionEventArgs args)
    {
        _subscriptions.AddOrUpdate(args.TopicFilter.Topic, 1, (_, count) => count + 1);
        _topicStats.AddOrUpdate(
            args.TopicFilter.Topic,
            _ => new TopicStatistics { Topic = args.TopicFilter.Topic, SubscriberCount = 1 },
            (_, stats) => new TopicStatistics
            {
                Topic = stats.Topic,
                SubscriberCount = stats.SubscriberCount + 1,
                MessageCount = stats.MessageCount,
                TotalBytes = stats.TotalBytes,
                LastMessageTime = stats.LastMessageTime
            });
        return Task.CompletedTask;
    }

    public Task MqttServerOnInterceptingUnsubscriptionAsync(InterceptingUnsubscriptionEventArgs args)
    {
        _subscriptions.AddOrUpdate(args.Topic, 0, (_, count) => Math.Max(count - 1, 0));
        _topicStats.AddOrUpdate(
            args.Topic,
            _ => new TopicStatistics { Topic = args.Topic, SubscriberCount = 0 },
            (_, stats) => new TopicStatistics
            {
                Topic = stats.Topic,
                SubscriberCount = Math.Max(stats.SubscriberCount - 1, 0),
                MessageCount = stats.MessageCount,
                TotalBytes = stats.TotalBytes,
                LastMessageTime = stats.LastMessageTime
            });
        return Task.CompletedTask;
    }

    public Task MqttServerOnInterceptingPublishAsync(InterceptingPublishEventArgs args, bool isServer)
    {
        var messageSize = (ulong)args.ApplicationMessage.Payload.Length;
        Interlocked.Increment(ref _totalMessages);
        Interlocked.Add(ref _totalBytesTransferred, messageSize);
        if (args.ApplicationMessage.Retain) Interlocked.Add(ref _retainedMessages, 1);

        if (isServer)
        {
            Interlocked.Add(ref _bytesSentByBroker, messageSize);
            Interlocked.Add(ref _bytesReceivedByClients, messageSize);
        }
        else
        {
            Interlocked.Add(ref _bytesReceivedByBroker, messageSize);
            Interlocked.Add(ref _bytesSentByClients, messageSize);
        }

        var now = DateTime.UtcNow;
        _topicStats.AddOrUpdate(
            args.ApplicationMessage.Topic,
            _ => new TopicStatistics { Topic = args.ApplicationMessage.Topic, MessageCount = 1, TotalBytes = messageSize, LastMessageTime = now },
            (_, stats) => new TopicStatistics
            {
                Topic = stats.Topic,
                SubscriberCount = stats.SubscriberCount,
                MessageCount = stats.MessageCount + 1,
                TotalBytes = stats.TotalBytes + messageSize,
                LastMessageTime = now
            });
        return Task.CompletedTask;
    }

    public Task MqttServerOnValidatingConnectionAsync(ValidatingConnectionEventArgs args)
    {
        _mqttSessions[args.ClientId] = true;
        return Task.CompletedTask;
    }

    public Task MqttServerOnSessionDeletedAsync(SessionDeletedEventArgs args)
    {
        _mqttSessions.TryRemove(args.Id, out _);
        return Task.CompletedTask;
    }

    public ulong GetTotalMessageCount() => _totalMessages;
    public ulong GetTotalMessageSize() => _totalBytesTransferred;
    public ulong GetTotalConnectedClientCount() => _connectedClients;
    public ulong GetRetainedMessageCount() => _retainedMessages;
    public int GetSessionCount() => _mqttSessions.Count;
    public int GetSubscriptionCount() => _subscriptions.Count;
    public Dictionary<string, TopicStatistics> GetTopicSummary() => new(_topicStats);
    public bool GetAcceptNewConnections() => _acceptNewConnections;
    public void SetAcceptNewConnections(bool accept) => _acceptNewConnections = accept;
    public void MarkBrokerStarted() => _brokerStartTime = DateTime.UtcNow;
    public TimeSpan GetUptime() => _brokerStartTime.HasValue ? DateTime.UtcNow - _brokerStartTime.Value : TimeSpan.Zero;
    public ulong GetTotalBytesReceivedByBroker() => _bytesReceivedByBroker;
    public ulong GetTotalBytesSentByBroker() => _bytesSentByBroker;
    public ulong GetTotalBytesReceivedByClients() => _bytesReceivedByClients;
    public ulong GetTotalBytesSentByClients() => _bytesSentByClients;
    public void IncrementDroppedMessageCount() => Interlocked.Increment(ref _droppedMessages);
    public ulong GetDroppedMessageCount() => _droppedMessages;
    public void IncrementUnconsumedMessageCount() => Interlocked.Increment(ref _unconsumedMessages);
    public ulong GetUnconsumedMessageCount() => _unconsumedMessages;
    public void IncrementAcknowledgedMessageCount() => Interlocked.Increment(ref _acknowledgedMessages);
    public ulong GetAcknowledgedMessageCount() => _acknowledgedMessages;
    public void IncrementQueueOverwriteCount() => Interlocked.Increment(ref _queueOverwrites);
    public ulong GetQueueOverwriteCount() => _queueOverwrites;
}
