using MQTTnet.Server;

namespace MqttControllerFramework.Multitenancy;

/// <summary>
///     Type-erasing internal abstraction so <c>MqttBrokerHostedService</c>
///     can call the typed resolver without knowing the tenant type at compile time.
/// </summary>
internal interface IMqttTenantResolverRunner
{
    Task RunAsync(ValidatingConnectionEventArgs ctx, CancellationToken ct);
}

internal sealed class MqttTenantResolverRunner<T>(IMqttTenantResolver<T> resolver)
    : IMqttTenantResolverRunner where T : class
{
    public async Task RunAsync(ValidatingConnectionEventArgs ctx, CancellationToken ct)
    {
        var tenant = await resolver.ResolveAsync(ctx.UserName, ctx.ClientId, ct);
        if (tenant != null)
            ctx.SessionItems[MqttTenantConstants.TenantInfoKey] = tenant;
    }
}
