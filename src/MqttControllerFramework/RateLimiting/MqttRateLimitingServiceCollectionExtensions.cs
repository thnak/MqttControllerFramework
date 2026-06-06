using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MqttControllerFramework.RateLimiting.Abstractions;
using MqttControllerFramework.RateLimiting.Strategies;

namespace MqttControllerFramework.RateLimiting;

/// <summary>Extension methods for enabling MQTT rate limiting.</summary>
public static class MqttRateLimitingServiceCollectionExtensions
{
    /// <summary>
    ///     Adds <see cref="IMqttRateLimitService"/> and the built-in token-bucket strategy.
    ///     Call after <c>AddMqttControllers</c>.
    /// </summary>
    public static IServiceCollection AddMqttRateLimiting(this IServiceCollection services)
    {
        services.TryAddSingleton<IMqttRateLimitService, MqttRateLimitService>();

        services.TryAddSingleton<IRateLimitStrategy>(sp =>
            new TokenBucketRateLimitStrategy(sp.GetRequiredService<TimeProvider>()));

        services.AddHostedService<MqttRateLimitingInitializationService>();

        return services;
    }
}
