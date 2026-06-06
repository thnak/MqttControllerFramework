using MqttControllerFramework.Authentication;

namespace MqttControllerFramework.Tests.Authentication;

public sealed class MqttAuthenticationResultTests
{
    [Fact]
    public void Authenticated_SetsIsAuthenticatedTrue()
    {
        var result = MqttAuthenticationResult.Authenticated();

        result.IsAuthenticated.Should().BeTrue();
        result.FailureReason.Should().BeNull();
        result.IsLocked.Should().BeFalse();
        result.LockTimeRemaining.Should().BeNull();
    }

    [Fact]
    public void Failed_SetsIsAuthenticatedFalseAndReason()
    {
        var result = MqttAuthenticationResult.Failed("bad password");

        result.IsAuthenticated.Should().BeFalse();
        result.FailureReason.Should().Be("bad password");
        result.IsLocked.Should().BeFalse();
        result.LockTimeRemaining.Should().BeNull();
    }

    [Fact]
    public void Locked_SetsIsLockedAndDuration()
    {
        var lockDuration = TimeSpan.FromMinutes(5);
        var result = MqttAuthenticationResult.Locked(lockDuration);

        result.IsAuthenticated.Should().BeFalse();
        result.IsLocked.Should().BeTrue();
        result.LockTimeRemaining.Should().Be(lockDuration);
        result.FailureReason.Should().NotBeNullOrEmpty();
    }
}
