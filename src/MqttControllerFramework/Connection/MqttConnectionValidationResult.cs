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

    /// <summary>Returns an accepted result.</summary>
    public static MqttConnectionValidationResult Accept() => new() { IsValid = true };

    /// <summary>Returns a rejected result.</summary>
    public static MqttConnectionValidationResult Reject(
        string reason,
        MqttConnectReasonCode code = MqttConnectReasonCode.NotAuthorized)
        => new() { IsValid = false, RejectReason = reason, RejectReasonCode = code };
}
