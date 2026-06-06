using MQTTnet.Server;

namespace MqttControllerFramework.Routing;

/// <summary>
///     Implemented by the source-generated routing service.
///     Responsible for matching incoming MQTT topics to controller methods and dispatching them.
/// </summary>
public interface IMqttRoutingService
{
    /// <summary>Returns <c>true</c> if a route is registered for <paramref name="topic"/>.</summary>
    bool IsRouteRegistered(string topic);

    /// <summary>
    ///     Intercepts an incoming publish event, matches the topic, and dispatches
    ///     to the appropriate controller method.
    /// </summary>
    ValueTask InterceptMessageAsync(InterceptingPublishEventArgs args, IServiceProvider serviceProvider);
}
