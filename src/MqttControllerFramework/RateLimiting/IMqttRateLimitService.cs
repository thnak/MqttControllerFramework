using MqttControllerFramework.RateLimiting.Abstractions;

namespace MqttControllerFramework.RateLimiting;

/// <summary>
///     Central service that evaluates rate-limit rules against incoming messages.
/// </summary>
public interface IMqttRateLimitService
{
    /// <summary>Checks whether a message from <paramref name="username"/> on <paramref name="topic"/> is allowed.</summary>
    Task<RateLimitResult> CheckRateLimitAsync(
        string username,
        string topic,
        long payloadSize,
        CancellationToken cancellationToken = default);

    /// <summary>Registers rate-limit configuration for a topic template (called at startup).</summary>
    void RegisterTopicRateLimits(string topicTemplate, IReadOnlyList<RouteRateLimitConfig> configs);

    /// <summary>Registers a custom strategy implementation.</summary>
    void RegisterStrategy(IRateLimitStrategy strategy);
}
