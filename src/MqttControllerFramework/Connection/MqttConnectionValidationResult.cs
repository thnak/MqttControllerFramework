using MQTTnet.Protocol;

namespace MqttControllerFramework.Connection;

/// <summary>Result returned by <see cref="IMqttConnectionValidator"/>.</summary>
public sealed class MqttConnectionValidationResult
{
    /// <summary>Whether the connection is accepted.</summary>
    public bool IsValid { get; init; }

    /// <summary>Human-readable rejection reason (logged, not sent to client).</summary>
    public string? RejectReason { get; init; }

    /// <summary>MQTT reason code sent to the client when the connection is rejected.</summary>
    public MqttConnectReasonCode RejectReasonCode { get; init; } = MqttConnectReasonCode.NotAuthorized;

    /// <summary>
    ///     When <c>true</c>, the broker skips the <see cref="Authentication.IMqttAuthenticationProvider"/>
    ///     step. Use this when the validator has already verified the client (e.g. by ClientId lookup)
    ///     and a separate username/password check is redundant.
    /// </summary>
    public bool BypassAuthentication { get; init; }

    /// <summary>Returns an accepted result.</summary>
    public static MqttConnectionValidationResult Accept() => new() { IsValid = true };

    /// <summary>
    ///     Returns an accepted result that also skips username/password authentication.
    ///     Use when the validator has already verified the client by other means (e.g. ClientId).
    /// </summary>
    public static MqttConnectionValidationResult AcceptAndBypassAuth() => new() { IsValid = true, BypassAuthentication = true };

    /// <summary>Returns a rejected result.</summary>
    public static MqttConnectionValidationResult Reject(
        string reason,
        MqttConnectReasonCode code = MqttConnectReasonCode.NotAuthorized)
        => new() { IsValid = false, RejectReason = reason, RejectReasonCode = code };
}
