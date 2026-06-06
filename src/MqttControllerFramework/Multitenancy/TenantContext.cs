namespace MqttControllerFramework.Multitenancy;

/// <summary>Default scoped implementation of <see cref="ITenantContext"/>.</summary>
public sealed class TenantContext : ITenantContext
{
    /// <inheritdoc />
    public string? TenantId { get; private set; }

    /// <inheritdoc />
    public void SetTenant(string tenantId) => TenantId = tenantId;
}

/// <summary>Default scoped implementation of <see cref="ITenantContext{T}"/>.</summary>
public sealed class TenantContext<T> : ITenantContext<T> where T : class
{
    /// <inheritdoc />
    public T? Tenant { get; private set; }

    /// <inheritdoc />
    public void SetTenant(T tenant) => Tenant = tenant;
}
