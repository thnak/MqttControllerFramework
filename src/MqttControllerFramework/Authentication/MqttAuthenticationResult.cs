namespace MqttControllerFramework.Authentication;

/// <summary>Result returned by <see cref="IMqttAuthenticationProvider"/>.</summary>
public sealed class MqttAuthenticationResult
{
    /// <summary>Whether authentication succeeded.</summary>
    public bool IsAuthenticated { get; init; }

    /// <summary>Human-readable failure reason when <see cref="IsAuthenticated"/> is <c>false</c>.</summary>
    public string? FailureReason { get; init; }

    /// <summary>Whether the client is temporarily locked out due to repeated failures.</summary>
    public bool IsLocked { get; init; }

    /// <summary>How long until the lockout expires. <c>null</c> when not locked.</summary>
    public TimeSpan? LockTimeRemaining { get; init; }

    /// <summary>Returns a successful authentication result.</summary>
    public static MqttAuthenticationResult Authenticated() => new() { IsAuthenticated = true };

    /// <summary>Returns a failed authentication result.</summary>
    public static MqttAuthenticationResult Failed(string reason) =>
        new() { IsAuthenticated = false, FailureReason = reason };

    /// <summary>Returns a locked-out result.</summary>
    public static MqttAuthenticationResult Locked(TimeSpan lockTimeRemaining) =>
        new() { IsAuthenticated = false, IsLocked = true, LockTimeRemaining = lockTimeRemaining, FailureReason = "Account is temporarily locked." };
}
