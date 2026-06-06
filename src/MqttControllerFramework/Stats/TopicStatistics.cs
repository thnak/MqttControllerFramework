namespace MqttControllerFramework.Stats;

/// <summary>Per-topic message statistics.</summary>
public sealed class TopicStatistics
{
    /// <summary>Topic name.</summary>
    public required string Topic { get; init; }

    /// <summary>Number of active subscribers.</summary>
    public int SubscriberCount { get; set; }

    /// <summary>Total messages published.</summary>
    public ulong MessageCount { get; set; }

    /// <summary>Total bytes transferred.</summary>
    public ulong TotalBytes { get; set; }

    /// <summary>Timestamp of the most recent message.</summary>
    public DateTime? LastMessageTime { get; set; }
}
