namespace MqttControllerFramework.RateLimiting.Abstractions;

/// <summary>Outcome of a rate-limit check.</summary>
public sealed class RateLimitResult
{
    /// <summary>Whether the message is allowed through.</summary>
    public bool IsAllowed { get; init; }

    /// <summary>Denial reason when <see cref="IsAllowed"/> is <c>false</c>.</summary>
    public string? DenialReason { get; init; }

    /// <summary>Suggested retry delay in seconds.</summary>
    public int? RetryAfterSeconds { get; init; }

    /// <summary>Additional metadata (token counts, capacities, etc.).</summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>Returns a passing result.</summary>
    public static RateLimitResult Allow() => new() { IsAllowed = true };

    /// <summary>Returns a denied result.</summary>
    public static RateLimitResult Deny(
        string reason,
        int? retryAfterSeconds = null,
        Dictionary<string, object>? metadata = null)
        => new()
        {
            IsAllowed = false,
            DenialReason = reason,
            RetryAfterSeconds = retryAfterSeconds,
            Metadata = metadata
        };
}
