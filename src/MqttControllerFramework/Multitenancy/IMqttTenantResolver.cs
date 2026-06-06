namespace MqttControllerFramework.Multitenancy;

/// <summary>
///     Resolves a tenant identifier string for an authenticated MQTT client.
///     Called once per connection, after authentication succeeds.
///     The resolved ID is stored in <see cref="MqttTenantConstants.SessionItemKey"/>
///     and made available to controllers via <see cref="ITenantContext"/>.
/// </summary>
/// <remarks>
///     Register with <c>builder.WithTenantResolver&lt;T&gt;()</c>.
///     For a fully-typed tenant object (e.g. Finbuckle's <c>AppTenantInfo</c>),
///     implement <see cref="IMqttTenantResolver{T}"/> instead.
/// </remarks>
public interface IMqttTenantResolver
{
    /// <summary>
    ///     Resolves the tenant ID string for a connecting client.
    ///     Returns <c>null</c> if the client has no tenant affiliation.
    /// </summary>
    ValueTask<string?> ResolveAsync(string username, string clientId, CancellationToken cancellationToken = default);
}

/// <summary>
///     Typed variant of <see cref="IMqttTenantResolver"/> that resolves a full tenant object.
///     Called once per connection, after authentication succeeds.
///     The resolved object is stored in <see cref="MqttTenantConstants.TenantInfoKey"/>
///     and made available to controllers via <see cref="ITenantContext{T}"/>.
/// </summary>
/// <typeparam name="T">Your tenant info class (e.g. <c>AppTenantInfo</c>).</typeparam>
/// <remarks>
///     Register with <c>builder.WithTenantResolver&lt;TResolver, TTenant&gt;()</c>.
///     Because the full object lives in <c>SessionItems</c>, it is resolved only once per
///     TCP connection — not per message — so downstream lookups (database, cache) pay
///     the cost exactly once.
/// </remarks>
/// <example>
/// // Implement:
/// public class AppTenantResolver(ITenantStore store) : IMqttTenantResolver&lt;AppTenantInfo&gt;
/// {
///     public async ValueTask&lt;AppTenantInfo?&gt; ResolveAsync(
///         string username, string clientId, CancellationToken ct)
///         => await store.FindByUsernameAsync(username, ct);
/// }
///
/// // Finbuckle bridge middleware (user-provided, no framework dep on Finbuckle):
/// public class FinbuckleMqttBridge(
///     ITenantContext&lt;AppTenantInfo&gt; tenantCtx,
///     IMultiTenantContextSetter setter) : IMqttMiddleware
/// {
///     public Task InvokeAsync(MqttMessageContext ctx, MqttRequestDelegate next)
///     {
///         if (tenantCtx.Tenant is { } info)
///             setter.MultiTenantContext = new MultiTenantContext&lt;AppTenantInfo&gt; { TenantInfo = info };
///         return next(ctx);
///     }
/// }
///
/// // Register:
/// builder.Services
///     .AddMqttServer(cfg)
///     .WithTenantResolver&lt;AppTenantResolver, AppTenantInfo&gt;()
///     .UseMiddleware&lt;FinbuckleMqttBridge&gt;();
/// </example>
public interface IMqttTenantResolver<T> where T : class
{
    /// <summary>
    ///     Resolves the tenant object for a connecting client.
    ///     Returns <c>null</c> if the client has no tenant affiliation.
    /// </summary>
    ValueTask<T?> ResolveAsync(string username, string clientId, CancellationToken cancellationToken = default);
}
