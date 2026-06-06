namespace MqttControllerFramework.RateLimiting.Abstractions;

/// <summary>Context passed to a rate-limit strategy for a single message.</summary>
public sealed class RateLimitContext
{
    /// <summary>MQTT topic being published to.</summary>
    public required string Topic { get; init; }

    /// <summary>Username of the publishing client.</summary>
    public required string UserName { get; init; }

    /// <summary>Payload size in bytes.</summary>
    public long PayloadSize { get; init; }

    /// <summary>Strategy-specific configuration and extra properties.</summary>
    public Dictionary<string, object>? Properties { get; init; }
}
