namespace MqttControllerFramework.RateLimiting;

/// <summary>
///     Serializable rate-limit configuration for a single route, produced by the source generator
///     from <see cref="Abstractions.RateLimitAttribute"/> annotations.
/// </summary>
public sealed class RouteRateLimitConfig
{
    /// <summary>Matches a registered <see cref="Abstractions.IRateLimitStrategy.StrategyType"/>.</summary>
    public required string StrategyType { get; init; }

    /// <summary>Lower values execute first.</summary>
    public int Priority { get; init; }

    /// <summary>Strategy-specific configuration dictionary.</summary>
    public required Dictionary<string, object> Configuration { get; init; }

    /// <summary>Optional display name.</summary>
    public string? Name { get; init; }
}
