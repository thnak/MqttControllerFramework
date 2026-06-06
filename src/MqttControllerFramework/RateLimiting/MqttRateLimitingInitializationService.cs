using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MqttControllerFramework.RateLimiting.Abstractions;
using MqttControllerFramework.Routing;

namespace MqttControllerFramework.RateLimiting;

/// <summary>
///     Hosted service that wires registered <see cref="IRateLimitStrategy"/> instances and
///     route-level rate-limit configs into <see cref="IMqttRateLimitService"/> at startup.
/// </summary>
internal sealed class MqttRateLimitingInitializationService : IHostedService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<MqttRateLimitingInitializationService> _logger;

    public MqttRateLimitingInitializationService(
        IServiceProvider sp,
        ILogger<MqttRateLimitingInitializationService> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var svc = _sp.GetService<IMqttRateLimitService>();
        if (svc == null)
        {
            _logger.LogWarning("IMqttRateLimitService not registered — skipping rate limit init.");
            return Task.CompletedTask;
        }

        var strategies = _sp.GetServices<IRateLimitStrategy>().ToList();
        foreach (var s in strategies) svc.RegisterStrategy(s);

        var router = _sp.GetService<MqttRouter>();
        if (router != null)
            foreach (var route in router.Routes.Values)
                if (route.RateLimitConfigs is { Count: > 0 })
                    svc.RegisterTopicRateLimits(route.TopicTemplate, route.RateLimitConfigs);

        _logger.LogInformation("MQTT rate limiting initialized — {Strategies} strategies, {Routes} rate-limited routes.",
            strategies.Count,
            router?.Routes.Values.Count(r => r.RateLimitConfigs is { Count: > 0 }) ?? 0);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
