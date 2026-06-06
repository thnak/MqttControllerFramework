using MQTTnet.Protocol;
using MqttControllerFramework.Connection;

namespace MqttControllerFramework.Tests.Connection;

public sealed class MqttConnectionValidationResultTests
{
    [Fact]
    public void Accept_SetsIsValidTrue()
    {
        var result = MqttConnectionValidationResult.Accept();

        result.IsValid.Should().BeTrue();
        result.RejectReason.Should().BeNull();
    }

    [Fact]
    public void Reject_SetsIsValidFalseAndReason()
    {
        var result = MqttConnectionValidationResult.Reject("banned client");

        result.IsValid.Should().BeFalse();
        result.RejectReason.Should().Be("banned client");
        result.RejectReasonCode.Should().Be(MqttConnectReasonCode.NotAuthorized);
    }

    [Fact]
    public void Reject_WithCustomCode_UsesCustomCode()
    {
        var result = MqttConnectionValidationResult.Reject("quota", MqttConnectReasonCode.QuotaExceeded);

        result.IsValid.Should().BeFalse();
        result.RejectReasonCode.Should().Be(MqttConnectReasonCode.QuotaExceeded);
    }
}
