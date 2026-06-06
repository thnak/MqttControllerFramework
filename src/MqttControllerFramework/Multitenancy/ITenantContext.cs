namespace MqttControllerFramework.Multitenancy;

/// <summary>
///     Scoped service that exposes the current tenant ID for the active MQTT message.
///     Populated by <see cref="TenantMiddleware"/> when using the string-based resolver path.
///     Inject into controllers or any scoped service.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    ///     The resolved tenant identifier, or <c>null</c> when the client
    ///     has no tenant affiliation or the resolver returned <c>null</c>.
    /// </summary>
    string? TenantId { get; }

    /// <summary>Sets the tenant identifier. Called internally by the framework.</summary>
    void SetTenant(string tenantId);
}

/// <summary>
///     Typed scoped service that exposes the full tenant object for the active MQTT message.
///     Populated by <see cref="TenantMiddleware{T}"/> when using the typed resolver path.
///     Inject into controllers or any scoped service.
/// </summary>
/// <typeparam name="T">Your tenant info class (e.g. <c>AppTenantInfo</c>).</typeparam>
public interface ITenantContext<T> where T : class
{
    /// <summary>
    ///     The resolved tenant object, or <c>null</c> when the client has no tenant affiliation.
    /// </summary>
    T? Tenant { get; }

    /// <summary>Sets the tenant object. Called internally by the framework.</summary>
    void SetTenant(T tenant);
}
