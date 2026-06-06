using Microsoft.Extensions.DependencyInjection;

namespace MqttControllerFramework;

/// <summary>
///     Implemented by the source-generated <c>GeneratedMqttControllerRegistration</c> class
///     in the consumer assembly. Pass the concrete type to
///     <see cref="Extensions.MqttControllerServiceCollectionExtensions.AddMqttControllers{TRegistration}"/>.
/// </summary>
public interface IMqttControllerRegistration
{
    /// <summary>Returns all discovered MQTT controller types.</summary>
    IReadOnlyList<Type> GetControllerTypes();

    /// <summary>Returns all MQTT routes with their metadata.</summary>
    IReadOnlyList<Routing.MqttRoute> GetRoutes();

    /// <summary>
    ///     Registers dispatcher services and the generated <c>IMqttRoutingService</c>
    ///     implementation into the DI container.
    /// </summary>
    void RegisterDispatchers(IServiceCollection services);
}
