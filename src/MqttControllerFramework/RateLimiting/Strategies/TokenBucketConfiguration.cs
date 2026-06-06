namespace MqttControllerFramework.RateLimiting.Strategies;

/// <summary>Validated configuration for the token-bucket algorithm.</summary>
public sealed class TokenBucketConfiguration
{
    /// <summary>Maximum tokens (burst capacity).</summary>
    public int Capacity { get; init; }

    /// <summary>Tokens added per refill interval.</summary>
    public int RefillRate { get; init; }

    /// <summary>Refill interval in milliseconds.</summary>
    public int RefillIntervalMs { get; init; }

    /// <summary>Tokens consumed per message.</summary>
    public int TokensPerRequest { get; init; } = 1;

    /// <summary>Returns <c>true</c> when all values are in range.</summary>
    public bool IsValid()
        => Capacity > 0
           && RefillRate > 0
           && RefillIntervalMs > 0
           && TokensPerRequest > 0
           && TokensPerRequest <= Capacity;
}
