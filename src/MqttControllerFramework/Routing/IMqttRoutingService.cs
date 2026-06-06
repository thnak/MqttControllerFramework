using MQTTnet.Server;
using MqttControllerFramework.Pipeline;

namespace MqttControllerFramework.Routing;

/// <summary>
///     Implemented by the source-generated routing service.
///     Matches incoming MQTT topics to controller methods and dispatches them.
/// </summary>
public interface IMqttRoutingService
{
    /// <summary>Returns <c>true</c> if a route is registered for <paramref name="topic"/>.</summary>
    bool IsRouteRegistered(string topic);

    /// <summary>
    ///     Pipeline terminal — matches the topic and dispatches to the controller.
    ///     Called by the framework after all middleware have run.
    /// </summary>
    Task RouteAsync(MqttMessageContext context);

    /// <summary>
    ///     Legacy dispatch entry-point. Kept for backward compatibility.
    ///     New generated code overrides <see cref="RouteAsync"/> instead.
    /// </summary>
    ValueTask InterceptMessageAsync(InterceptingPublishEventArgs args, IServiceProvider serviceProvider)
        => ValueTask.CompletedTask;
}
