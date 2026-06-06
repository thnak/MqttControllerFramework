using System.Collections;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using MQTTnet.Server;
using MqttControllerFramework.Multitenancy;
using MqttControllerFramework.Pipeline;

namespace MqttControllerFramework.Tests.Multitenancy;

public sealed class TenantMiddlewareTests
{
    private static MqttMessageContext BuildContext(IServiceProvider services, IDictionary? sessionItems = null)
    {
        var message = new MqttApplicationMessage { Topic = "test/topic" };
        var items = sessionItems ?? new Hashtable();
        var args = new InterceptingPublishEventArgs(message, "client1", "user1", items, CancellationToken.None);
        return new MqttMessageContext { Args = args, Services = services };
    }

    // ── String path (TenantMiddleware) ─────────────────────────────────────

    [Fact]
    public async Task StringMiddleware_SetsTenantContext_WhenSessionItemPresent()
    {
        var tenantCtx = new TenantContext();
        var services = new ServiceCollection().BuildServiceProvider();
        var sessionItems = new Hashtable { [MqttTenantConstants.SessionItemKey] = "tenant-a" };
        var context = BuildContext(services, sessionItems);
        var middleware = new TenantMiddleware(tenantCtx);

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        tenantCtx.TenantId.Should().Be("tenant-a");
    }

    [Fact]
    public async Task StringMiddleware_LeavesContextNull_WhenSessionItemAbsent()
    {
        var tenantCtx = new TenantContext();
        var services = new ServiceCollection().BuildServiceProvider();
        var context = BuildContext(services);
        var middleware = new TenantMiddleware(tenantCtx);

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        tenantCtx.TenantId.Should().BeNull();
    }

    [Fact]
    public async Task StringMiddleware_CallsNext()
    {
        var tenantCtx = new TenantContext();
        var services = new ServiceCollection().BuildServiceProvider();
        var context = BuildContext(services);
        var middleware = new TenantMiddleware(tenantCtx);
        var called = false;

        await middleware.InvokeAsync(context, _ => { called = true; return Task.CompletedTask; });

        called.Should().BeTrue();
    }

    // ── Typed path (TenantMiddleware<T>) ───────────────────────────────────

    [Fact]
    public async Task TypedMiddleware_SetsTenantContext_WhenSessionItemPresent()
    {
        var tenantCtx = new TenantContext<FakeTenantInfo>();
        var services = new ServiceCollection().BuildServiceProvider();
        var info = new FakeTenantInfo("tenant-b");
        var sessionItems = new Hashtable { [MqttTenantConstants.TenantInfoKey] = info };
        var context = BuildContext(services, sessionItems);
        var middleware = new TenantMiddleware<FakeTenantInfo>(tenantCtx);

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        tenantCtx.Tenant.Should().BeSameAs(info);
        tenantCtx.Tenant!.Id.Should().Be("tenant-b");
    }

    [Fact]
    public async Task TypedMiddleware_LeavesContextNull_WhenSessionItemAbsent()
    {
        var tenantCtx = new TenantContext<FakeTenantInfo>();
        var services = new ServiceCollection().BuildServiceProvider();
        var context = BuildContext(services);
        var middleware = new TenantMiddleware<FakeTenantInfo>(tenantCtx);

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        tenantCtx.Tenant.Should().BeNull();
    }

    [Fact]
    public async Task TypedMiddleware_WrongTypeInSessionItems_LeavesContextNull()
    {
        var tenantCtx = new TenantContext<FakeTenantInfo>();
        var services = new ServiceCollection().BuildServiceProvider();
        var sessionItems = new Hashtable { [MqttTenantConstants.TenantInfoKey] = "not-a-FakeTenantInfo" };
        var context = BuildContext(services, sessionItems);
        var middleware = new TenantMiddleware<FakeTenantInfo>(tenantCtx);

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        tenantCtx.Tenant.Should().BeNull();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private sealed record FakeTenantInfo(string Id);
}
