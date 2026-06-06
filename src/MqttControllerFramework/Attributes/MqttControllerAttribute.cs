namespace MqttControllerFramework.Attributes;

/// <summary>
///     Marks a class as an MQTT controller and optionally defines a topic prefix
///     prepended to all routes within the controller.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MqttControllerAttribute : Attribute
{
    /// <param name="prefix">Optional base topic path, e.g. <c>"device"</c> or <c>"system/logs"</c>.</param>
    public MqttControllerAttribute(string prefix = "")
    {
        Prefix = prefix;
    }

    /// <summary>Topic prefix prepended to all method topic templates in this controller.</summary>
    public string Prefix { get; }
}
