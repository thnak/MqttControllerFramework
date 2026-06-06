namespace MqttControllerFramework.Pipeline;

/// <summary>
///     Defines a middleware component in the MQTT message processing pipeline.
///     Middleware runs in registration order before the controller dispatcher.
/// </summary>
/// <remarks>
///     Register implementations via <c>MqttServerBuilder.UseMiddleware&lt;T&gt;()</c>.
///     The framework resolves all registered <see cref="IMqttMiddleware"/> instances from the
///     per-message DI scope, so both scoped and singleton lifetimes are supported.
/// </remarks>
/// <example>
/// // Multi-tenant resolution middleware:
/// public class TenantMiddleware(ITenantContext tenantCtx) : IMqttMiddleware
/// {
///     public Task InvokeAsync(MqttMessageContext context, MqttRequestDelegate next)
///     {
///         if (context.SessionItems.TryGetValue("tenantId", out var tid))
///             tenantCtx.SetTenant((string)tid!);
///         return next(context);
///     }
/// }
/// </example>
public interface IMqttMiddleware
{
    /// <summary>
    ///     Processes the message.
    ///     Call <paramref name="next"/> to continue the pipeline,
    ///     or return without calling it to short-circuit (e.g. drop the message).
    /// </summary>
    Task InvokeAsync(MqttMessageContext context, MqttRequestDelegate next);
}
