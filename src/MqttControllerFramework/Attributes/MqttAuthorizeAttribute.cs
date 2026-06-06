namespace MqttControllerFramework.Attributes;

/// <summary>
///     Requires authorization for an MQTT topic handler.
///     Applied at method or class level; class-level is inherited by all methods.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class MqttAuthorizeAttribute : Attribute
{
    /// <summary>Initializes a new instance with no role or policy constraints.</summary>
    public MqttAuthorizeAttribute() { }

    /// <param name="role">Single role that is authorized to access this handler.</param>
    public MqttAuthorizeAttribute(string role)
    {
        Roles = role;
    }

    /// <summary>Comma-separated list of allowed roles.</summary>
    public string? Roles { get; set; }

    /// <summary>Comma-separated list of required policies.</summary>
    public string? Policies { get; set; }

    /// <summary>Named authorization policy to apply.</summary>
    public string? Policy { get; set; }

    /// <summary>
    ///     When <c>true</c>, all specified roles are required (AND logic).
    ///     Default <c>false</c> means any single role is sufficient (OR logic).
    /// </summary>
    public bool RequireAllRoles { get; set; }

    /// <summary>Custom message returned to the client when authorization fails.</summary>
    public string? UnauthorizedMessage { get; set; }
}
