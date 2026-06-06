namespace MqttControllerFramework.Attributes;

/// <summary>
///     Maps a method to an MQTT topic template. Supports MQTT wildcards:
///     <c>+</c> (single-level) and <c>#</c> (multi-level).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class MqttTopicAttribute : Attribute
{
    /// <param name="template">Topic template, e.g. <c>"devices/+/telemetry"</c>.</param>
    public MqttTopicAttribute(string template)
    {
        Template = template;
    }

    /// <summary>The topic template for this handler.</summary>
    public string Template { get; }
}
