using MqttControllerFramework.RateLimiting.Abstractions;

namespace MqttControllerFramework.RateLimiting.Attributes;

/// <summary>
///     Applies token-bucket rate limiting to a topic handler.
///     Multiple instances may be stacked on a single method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class TokenBucketRateLimitAttribute : RateLimitAttribute
{
    /// <param name="capacity">Max tokens (burst).</param>
    /// <param name="refillRate">Tokens added per refill interval.</param>
    /// <param name="refillIntervalMs">Refill interval in milliseconds.</param>
    public TokenBucketRateLimitAttribute(int capacity, int refillRate, int refillIntervalMs)
    {
        Capacity = capacity;
        RefillRate = refillRate;
        RefillIntervalMs = refillIntervalMs;
    }

    /// <inheritdoc />
    public override string StrategyType => "TokenBucket";

    /// <summary>Maximum tokens in the bucket.</summary>
    public int Capacity { get; }

    /// <summary>Tokens added per interval.</summary>
    public int RefillRate { get; }

    /// <summary>Refill interval in milliseconds.</summary>
    public int RefillIntervalMs { get; }

    /// <summary>Tokens consumed per message. Default: 1.</summary>
    public int TokensPerRequest { get; set; } = 1;

    /// <inheritdoc />
    public override bool ValidateConfiguration()
        => Capacity > 0 && RefillRate > 0 && RefillIntervalMs > 0
           && TokensPerRequest > 0 && TokensPerRequest <= Capacity;

    /// <inheritdoc />
    public override Dictionary<string, object> GetConfiguration()
        => new()
        {
            ["Capacity"] = Capacity,
            ["RefillRate"] = RefillRate,
            ["RefillIntervalMs"] = RefillIntervalMs,
            ["TokensPerRequest"] = TokensPerRequest
        };
}
