namespace MqttControllerFramework.RateLimiting.Abstractions;

/// <summary>
///     A pluggable rate-limiting algorithm. Register custom implementations with DI
///     and they will be picked up by <see cref="IMqttRateLimitService"/>.
/// </summary>
public interface IRateLimitStrategy
{
    /// <summary>Unique identifier matching <see cref="RateLimitAttribute.StrategyType"/>.</summary>
    string StrategyType { get; }

    /// <summary>Checks whether the current message should be allowed.</summary>
    Task<RateLimitResult> CheckRateLimitAsync(RateLimitContext context, CancellationToken cancellationToken = default);

    /// <summary>Resets the rate-limit state for a specific client / topic pair.</summary>
    Task ResetAsync(string username, string topic, CancellationToken cancellationToken = default);

    /// <summary>Returns diagnostic statistics for a client / topic pair.</summary>
    Task<Dictionary<string, object>> GetStatisticsAsync(string clientId, string topic, CancellationToken cancellationToken = default);
}
