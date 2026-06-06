namespace MqttControllerFramework.RateLimiting.Abstractions;

/// <summary>
///     Base attribute for applying rate limiting to a topic handler.
///     Derive this to create custom rate-limit attributes (e.g. <c>TokenBucketRateLimitAttribute</c>).
///     Multiple attributes may be applied to a single method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public abstract class RateLimitAttribute : Attribute
{
    /// <summary>Matches a registered <see cref="IRateLimitStrategy.StrategyType"/>.</summary>
    public abstract string StrategyType { get; }

    /// <summary>Lower values execute first.</summary>
    public int Priority { get; set; } = 100;

    /// <summary>Optional display name for this limiter instance.</summary>
    public string? Name { get; set; }

    /// <summary>Returns <c>false</c> when the attribute's parameters are invalid.</summary>
    public virtual bool ValidateConfiguration() => true;

    /// <summary>Returns the configuration dictionary passed to the strategy at runtime.</summary>
    public abstract Dictionary<string, object> GetConfiguration();
}
