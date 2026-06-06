using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MqttControllerFramework.RateLimiting.Abstractions;

namespace MqttControllerFramework.RateLimiting;

/// <summary>Default <see cref="IMqttRateLimitService"/> implementation.</summary>
public sealed class MqttRateLimitService : IMqttRateLimitService
{
    private readonly ILogger<MqttRateLimitService> _logger;
    private readonly ConcurrentDictionary<string, IRateLimitStrategy> _strategies = new();
    private readonly ConcurrentDictionary<string, IReadOnlyList<RouteRateLimitConfig>> _topicConfigs = new();

    public MqttRateLimitService(ILogger<MqttRateLimitService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void RegisterStrategy(IRateLimitStrategy strategy)
    {
        if (_strategies.TryAdd(strategy.StrategyType, strategy))
            _logger.LogInformation("Registered rate limit strategy: {StrategyType}", strategy.StrategyType);
        else
            _logger.LogWarning("Rate limit strategy {StrategyType} already registered", strategy.StrategyType);
    }

    /// <inheritdoc />
    public void RegisterTopicRateLimits(string topicTemplate, IReadOnlyList<RouteRateLimitConfig> configs)
    {
        if (configs.Count == 0) return;
        _topicConfigs[topicTemplate] = configs.OrderBy(c => c.Priority).ToList();
        _logger.LogInformation("Registered {Count} rate limiter(s) for topic: {Template}", configs.Count, topicTemplate);
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> CheckRateLimitAsync(
        string username, string topic, long payloadSize, CancellationToken cancellationToken = default)
    {
        var configs = FindConfigs(topic);
        if (configs == null || configs.Count == 0) return RateLimitResult.Allow();

        foreach (var config in configs)
        {
            if (!_strategies.TryGetValue(config.StrategyType, out var strategy))
            {
                _logger.LogWarning("Rate limit strategy {Type} not found for topic {Topic}", config.StrategyType, topic);
                continue;
            }

            var ctx = new RateLimitContext
            {
                Topic = topic,
                UserName = username,
                PayloadSize = payloadSize,
                Properties = new Dictionary<string, object> { ["Configuration"] = config.Configuration }
            };

            var result = await strategy.CheckRateLimitAsync(ctx, cancellationToken);
            if (!result.IsAllowed)
            {
                _logger.LogWarning("Rate limit {Type} denied {User} on {Topic}: {Reason}",
                    config.StrategyType, username, topic, result.DenialReason);
                return result;
            }
        }

        return RateLimitResult.Allow();
    }

    private IReadOnlyList<RouteRateLimitConfig>? FindConfigs(string topic)
    {
        if (_topicConfigs.TryGetValue(topic, out var exact)) return exact;
        foreach (var kvp in _topicConfigs)
            if (TopicMatches(topic, kvp.Key)) return kvp.Value;
        return null;
    }

    private static bool TopicMatches(string topic, string template)
    {
        var tp = topic.Split('/');
        var tm = template.Split('/');
        for (var i = 0; i < tm.Length; i++)
        {
            if (tm[i] == "#") return true;
            if (i >= tp.Length) return false;
            if (tm[i] != "+" && tm[i] != tp[i]) return false;
        }
        return tp.Length == tm.Length || (tm.Length > 0 && tm[^1] == "#");
    }
}
