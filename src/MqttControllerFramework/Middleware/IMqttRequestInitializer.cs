using MQTTnet.Server;

namespace MqttControllerFramework.Middleware;

/// <summary>
///     Optional hook called before a message is dispatched to its controller.
///     Register an implementation to perform per-request initialization such as
///     resolving a tenant, setting ambient context, or populating DI scope data.
/// </summary>
public interface IMqttRequestInitializer
{
    /// <summary>
    ///     Called once per incoming message, after route matching and before
    ///     controller dispatch.
    /// </summary>
    ValueTask InitializeAsync(
        InterceptingPublishEventArgs args,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default);
}
