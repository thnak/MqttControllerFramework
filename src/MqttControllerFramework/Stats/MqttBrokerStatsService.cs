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

    /// <inheritdoc/>
    public Task MqttServerOnClientConnectedAsync(ClientConnectedEventArgs args)
    {
        Interlocked.Increment(ref _connectedClients);
        _mqttSessions.TryAdd(args.ClientId, false);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task MqttServerOnClientDisconnectedAsync(ClientDisconnectedEventArgs args)
    {
        Interlocked.Decrement(ref _connectedClients);
        _mqttSessions.TryRemove(args.ClientId, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public Task MqttServerOnValidatingConnectionAsync(ValidatingConnectionEventArgs args)
    {
        _mqttSessions[args.ClientId] = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task MqttServerOnSessionDeletedAsync(SessionDeletedEventArgs args)
    {
        _mqttSessions.TryRemove(args.Id, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public ulong GetTotalMessageCount() => _totalMessages;
    /// <inheritdoc/>
    public ulong GetTotalMessageSize() => _totalBytesTransferred;
    /// <inheritdoc/>
    public ulong GetTotalConnectedClientCount() => _connectedClients;
    /// <inheritdoc/>
    public ulong GetRetainedMessageCount() => _retainedMessages;
    /// <inheritdoc/>
    public int GetSessionCount() => _mqttSessions.Count;
    /// <inheritdoc/>
    public int GetSubscriptionCount() => _subscriptions.Count;
    /// <inheritdoc/>
    public Dictionary<string, TopicStatistics> GetTopicSummary() => new(_topicStats);
    /// <inheritdoc/>
    public bool GetAcceptNewConnections() => _acceptNewConnections;
    /// <inheritdoc/>
    public void SetAcceptNewConnections(bool accept) => _acceptNewConnections = accept;
    /// <inheritdoc/>
    public void MarkBrokerStarted() => _brokerStartTime = DateTime.UtcNow;
    /// <inheritdoc/>
    public TimeSpan GetUptime() => _brokerStartTime.HasValue ? DateTime.UtcNow - _brokerStartTime.Value : TimeSpan.Zero;
    /// <inheritdoc/>
    public ulong GetTotalBytesReceivedByBroker() => _bytesReceivedByBroker;
    /// <inheritdoc/>
    public ulong GetTotalBytesSentByBroker() => _bytesSentByBroker;
    /// <inheritdoc/>
    public ulong GetTotalBytesReceivedByClients() => _bytesReceivedByClients;
    /// <inheritdoc/>
    public ulong GetTotalBytesSentByClients() => _bytesSentByClients;
    /// <inheritdoc/>
    public void IncrementDroppedMessageCount() => Interlocked.Increment(ref _droppedMessages);
    /// <inheritdoc/>
    public ulong GetDroppedMessageCount() => _droppedMessages;
    /// <inheritdoc/>
    public void IncrementUnconsumedMessageCount() => Interlocked.Increment(ref _unconsumedMessages);
    /// <inheritdoc/>
    public ulong GetUnconsumedMessageCount() => _unconsumedMessages;
    /// <inheritdoc/>
    public void IncrementAcknowledgedMessageCount() => Interlocked.Increment(ref _acknowledgedMessages);
    /// <inheritdoc/>
    public ulong GetAcknowledgedMessageCount() => _acknowledgedMessages;
    /// <inheritdoc/>
    public void IncrementQueueOverwriteCount() => Interlocked.Increment(ref _queueOverwrites);
    /// <inheritdoc/>
    public ulong GetQueueOverwriteCount() => _queueOverwrites;
}
