using System.Collections.Concurrent;
using MqttControllerFramework.RateLimiting.Abstractions;

namespace MqttControllerFramework.RateLimiting.Strategies;

/// <summary>Token-bucket rate limiting strategy. One bucket per client/topic pair.</summary>
public sealed class TokenBucketRateLimitStrategy : IRateLimitStrategy
{
    private readonly ConcurrentDictionary<string, TokenBucket> _buckets = new();
    private readonly TimeProvider _time;

    /// <summary>Initializes a new instance using the supplied <paramref name="time"/> provider for refill calculations.</summary>
    public TokenBucketRateLimitStrategy(TimeProvider time)
    {
        _time = time;
    }

    /// <inheritdoc />
    public string StrategyType => "TokenBucket";

    /// <inheritdoc />
    public Task<RateLimitResult> CheckRateLimitAsync(RateLimitContext context, CancellationToken cancellationToken = default)
    {
        if (context.Properties == null || !context.Properties.TryGetValue("Configuration", out var configObj))
            return Task.FromResult(RateLimitResult.Deny("Missing rate limit configuration"));

        TokenBucketConfiguration config;
        if (configObj is Dictionary<string, object> dict)
        {
            config = new TokenBucketConfiguration
            {
                Capacity = GetInt(dict, "Capacity"),
                RefillRate = GetInt(dict, "RefillRate"),
                RefillIntervalMs = GetInt(dict, "RefillIntervalMs"),
                TokensPerRequest = GetInt(dict, "TokensPerRequest", 1)
            };
        }
        else if (configObj is TokenBucketConfiguration tbc)
        {
            config = tbc;
        }
        else
        {
            return Task.FromResult(RateLimitResult.Deny("Invalid rate limit configuration type"));
        }

        if (!config.IsValid())
            return Task.FromResult(RateLimitResult.Deny("Invalid rate limit configuration"));

        var key = $"{context.UserName}:{context.Topic}";
        var bucket = _buckets.GetOrAdd(key, _ => new TokenBucket(config, _time));

        if (bucket.TryConsume(config.TokensPerRequest))
            return Task.FromResult(RateLimitResult.Allow());

        var retry = bucket.CalculateRetryAfter(config.TokensPerRequest);
        var meta = new Dictionary<string, object>
        {
            ["AvailableTokens"] = bucket.GetAvailableTokens(),
            ["Capacity"] = config.Capacity,
            ["RefillRate"] = config.RefillRate,
            ["RefillIntervalMs"] = config.RefillIntervalMs
        };
        return Task.FromResult(RateLimitResult.Deny("Rate limit exceeded", retry, meta));
    }

    /// <inheritdoc />
    public Task ResetAsync(string username, string topic, CancellationToken cancellationToken = default)
    {
        if (_buckets.TryGetValue($"{username}:{topic}", out var bucket))
            bucket.Reset();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Dictionary<string, object>> GetStatisticsAsync(string clientId, string topic, CancellationToken cancellationToken = default)
    {
        var key = $"{clientId}:{topic}";
        var stats = new Dictionary<string, object>
        {
            ["ClientId"] = clientId,
            ["Topic"] = topic,
            ["BucketExists"] = _buckets.ContainsKey(key)
        };
        if (_buckets.TryGetValue(key, out var bucket))
            stats["AvailableTokens"] = bucket.GetAvailableTokens();
        return Task.FromResult(stats);
    }

    private static int GetInt(Dictionary<string, object> d, string key, int fallback = 0)
        => d.TryGetValue(key, out var v) ? v switch
        {
            int i => i,
            long l => (int)l,
            double dbl => (int)dbl,
            string s when int.TryParse(s, out var p) => p,
            _ => fallback
        } : fallback;
}
