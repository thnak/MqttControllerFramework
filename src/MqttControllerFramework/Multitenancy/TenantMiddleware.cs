using MqttControllerFramework.Pipeline;

namespace MqttControllerFramework.Multitenancy;

/// <summary>
///     Middleware that populates <see cref="ITenantContext"/> from
///     <see cref="MqttTenantConstants.SessionItemKey"/> — the string-based resolver path.
///     Auto-registered by <c>MqttServerBuilder.WithTenantResolver&lt;T&gt;()</c>.
/// </summary>
public sealed class TenantMiddleware(ITenantContext tenantContext) : IMqttMiddleware
{
    /// <inheritdoc />
    public Task InvokeAsync(MqttMessageContext context, MqttRequestDelegate next)
    {
        if (context.SessionItems[MqttTenantConstants.SessionItemKey] is string tenantId)
            tenantContext.SetTenant(tenantId);
        return next(context);
    }
}

/// <summary>
///     Middleware that populates <see cref="ITenantContext{T}"/> from
///     <see cref="MqttTenantConstants.TenantInfoKey"/> — the typed resolver path.
///     Auto-registered by <c>MqttServerBuilder.WithTenantResolver&lt;TResolver, TTenant&gt;()</c>.
/// </summary>
/// <typeparam name="T">Your tenant info class (e.g. <c>AppTenantInfo</c>).</typeparam>
public sealed class TenantMiddleware<T>(ITenantContext<T> tenantContext) : IMqttMiddleware where T : class
{
    /// <inheritdoc />
    public Task InvokeAsync(MqttMessageContext context, MqttRequestDelegate next)
    {
        if (context.SessionItems[MqttTenantConstants.TenantInfoKey] is T tenant)
            tenantContext.SetTenant(tenant);
        return next(context);
    }
}
