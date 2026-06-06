using MQTTnet.Server;

namespace MqttControllerFramework.Middleware;

/// <summary>
///     Optional hook called before a message is dispatched to its controller.
/// </summary>
/// <remarks>
///     <strong>Deprecated.</strong> Implement <see cref="Pipeline.IMqttMiddleware"/> instead and register it
///     via <c>MqttServerBuilder.UseMiddleware&lt;T&gt;()</c>.
///     <see cref="IMqttMiddleware"/> gives full control over the pipeline, access to
///     <see cref="Pipeline.MqttMessageContext.SessionItems"/> (for tenant resolution), and supports
///     short-circuiting.
/// </remarks>
[Obsolete("Use IMqttMiddleware + MqttServerBuilder.UseMiddleware<T>() instead.", error: false)]
public interface IMqttRequestInitializer
{
    /// <summary>Called once per incoming message, before controller dispatch.</summary>
    ValueTask InitializeAsync(
        InterceptingPublishEventArgs args,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default);
}
