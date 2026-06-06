using MqttControllerFramework.Multitenancy;

namespace MqttControllerFramework.Tests.Multitenancy;

public sealed class TenantContextTests
{
    [Fact]
    public void TenantId_IsNull_BeforeSetTenant()
    {
        var ctx = new TenantContext();
        ctx.TenantId.Should().BeNull();
    }

    [Fact]
    public void SetTenant_UpdatesTenantId()
    {
        var ctx = new TenantContext();
        ctx.SetTenant("tenant-a");
        ctx.TenantId.Should().Be("tenant-a");
    }

    [Fact]
    public void SetTenant_CanBeCalledMultipleTimes()
    {
        var ctx = new TenantContext();
        ctx.SetTenant("tenant-a");
        ctx.SetTenant("tenant-b");
        ctx.TenantId.Should().Be("tenant-b");
    }
}
