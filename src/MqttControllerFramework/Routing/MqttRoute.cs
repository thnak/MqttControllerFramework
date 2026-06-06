using System.Reflection;
using MqttControllerFramework.RateLimiting;

namespace MqttControllerFramework.Routing;

/// <summary>
///     Represents a registered MQTT route produced by the source generator.
/// </summary>
public sealed record MqttRoute
{
    /// <summary>Topic template (may contain <c>+</c> / <c>#</c> wildcards).</summary>
    public required string TopicTemplate { get; init; }

    /// <summary>Controller type that owns this route.</summary>
    public required Type ControllerType { get; init; }

    /// <summary>Handler method on the controller.</summary>
    public required MethodInfo HandlerMethod { get; init; }

    /// <summary>Mapping of wildcard index → parameter info for <c>[FromMqttTopic]</c> parameters.</summary>
    public required IReadOnlyDictionary<int, ParameterInfo> TopicParameters { get; init; }

    /// <summary>Optional generated dispatcher type for zero-reflection dispatch.</summary>
    public Type? DispatcherType { get; init; }

    /// <summary>Method name on the dispatcher to invoke.</summary>
    public string? DispatcherMethodName { get; init; }

    /// <summary>Rate-limit configurations, sorted by priority.</summary>
    public IReadOnlyList<RouteRateLimitConfig>? RateLimitConfigs { get; init; }

    /// <summary>Whether this route requires authorization.</summary>
    public bool RequiresAuthorization { get; init; }

    /// <summary>Whether anonymous access is explicitly allowed, overriding class-level auth.</summary>
    public bool AllowAnonymous { get; init; }

    /// <summary>Comma-separated required roles.</summary>
    public string? RequiredRoles { get; init; }

    /// <summary>Comma-separated required policies.</summary>
    public string? RequiredPolicies { get; init; }

    /// <summary>Named authorization policy.</summary>
    public string? AuthorizationPolicy { get; init; }

    /// <summary>When <c>true</c> all roles are required (AND); otherwise any role suffices (OR).</summary>
    public bool RequireAllRoles { get; init; }

    /// <summary>Custom message sent to the client when authorization fails.</summary>
    public string? UnauthorizedMessage { get; init; }
}
