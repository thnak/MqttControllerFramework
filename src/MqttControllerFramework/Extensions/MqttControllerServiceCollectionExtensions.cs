using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MqttControllerFramework.Routing;
using MqttControllerFramework.Serialization;

namespace MqttControllerFramework.Extensions;

/// <summary>Extension methods for registering the MQTT controller framework.</summary>
public static class MqttControllerServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the MQTT controller framework using the source-generated
    ///     <typeparamref name="TRegistration"/> class.
    /// </summary>
    /// <typeparam name="TRegistration">
    ///     The source-generated <c>GeneratedMqttControllerRegistration</c> type
    ///     from the consuming assembly. Must implement <see cref="IMqttControllerRegistration"/>
    ///     and have a public parameterless constructor.
    /// </typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureJsonOptions">Optional JSON serializer configuration.</param>
    public static IServiceCollection AddMqttControllers<TRegistration>(
        this IServiceCollection services,
        Action<JsonSerializerOptions>? configureJsonOptions = null)
        where TRegistration : IMqttControllerRegistration, new()
    {
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        configureJsonOptions?.Invoke(jsonOptions);
        services.TryAddSingleton(jsonOptions);

        services.TryAddSingleton<IMqttJsonTypeInfoCache, ConcurrentMqttJsonTypeInfoCache>();
        services.TryAddSingleton<IMqttPayloadParserRegistry, MqttPayloadParserRegistry>();

        var registration = new TRegistration();

        // Dispatchers + generated routing service → registered by the generated class
        registration.RegisterDispatchers(services);

        // Scoped controller instances
        foreach (var type in registration.GetControllerTypes())
            services.AddScoped(type);

        // Route table
        services.TryAddSingleton<MqttRouter>(sp =>
            new MqttRouter(
                sp.GetRequiredService<ILogger<MqttRouter>>(),
                registration.GetRoutes()));

        return services;
    }
}
