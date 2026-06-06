namespace MqttControllerFramework.Authorization;

/// <summary>
///     Result returned by <see cref="IMqttAuthorizationProvider"/>.
/// </summary>
public sealed class MqttAuthorizationResult
{
    /// <summary>Whether the request is authorized.</summary>
    public bool IsAuthorized { get; init; }

    /// <summary>Human-readable denial reason when <see cref="IsAuthorized"/> is <c>false</c>.</summary>
    public string? DenialReason { get; init; }

    /// <summary>Maximum QoS level the client is allowed to use. <c>null</c> means no constraint.</summary>
    public int? MaxQoS { get; init; }

    /// <summary>Whether the retain flag is permitted. <c>null</c> means no constraint.</summary>
    public bool? AllowRetain { get; init; }

    /// <summary>Returns an authorized result.</summary>
    public static MqttAuthorizationResult Allow() => new() { IsAuthorized = true };

    /// <summary>Returns a denied result with an optional QoS / retain constraint.</summary>
    public static MqttAuthorizationResult Deny(
        string reason,
        int? maxQoS = null,
        bool? allowRetain = null)
        => new()
        {
            IsAuthorized = false,
            DenialReason = reason,
            MaxQoS = maxQoS,
            AllowRetain = allowRetain
        };
}
