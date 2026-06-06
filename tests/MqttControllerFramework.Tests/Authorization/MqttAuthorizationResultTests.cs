using FluentAssertions;
using MqttControllerFramework.Authorization;

namespace MqttControllerFramework.Tests.Authorization;

public sealed class MqttAuthorizationResultTests
{
    [Fact]
    public void Allow_SetsIsAuthorizedTrue()
    {
        var result = MqttAuthorizationResult.Allow();

        result.IsAuthorized.Should().BeTrue();
        result.DenialReason.Should().BeNull();
        result.MaxQoS.Should().BeNull();
        result.AllowRetain.Should().BeNull();
    }

    [Fact]
    public void Deny_SetsIsAuthorizedFalseAndReason()
    {
        var result = MqttAuthorizationResult.Deny("not permitted");

        result.IsAuthorized.Should().BeFalse();
        result.DenialReason.Should().Be("not permitted");
        result.MaxQoS.Should().BeNull();
        result.AllowRetain.Should().BeNull();
    }

    [Fact]
    public void Deny_WithQosConstraint_SetsMaxQoS()
    {
        var result = MqttAuthorizationResult.Deny("qos limited", maxQoS: 1);

        result.IsAuthorized.Should().BeFalse();
        result.MaxQoS.Should().Be(1);
        result.AllowRetain.Should().BeNull();
    }

    [Fact]
    public void Deny_WithAllConstraints_SetsAllFields()
    {
        var result = MqttAuthorizationResult.Deny("restricted", maxQoS: 0, allowRetain: false);

        result.IsAuthorized.Should().BeFalse();
        result.DenialReason.Should().Be("restricted");
        result.MaxQoS.Should().Be(0);
        result.AllowRetain.Should().BeFalse();
    }
}
