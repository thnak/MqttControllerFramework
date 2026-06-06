namespace MqttControllerFramework.Attributes;

/// <summary>
///     Binds a method parameter to a wildcard segment in the MQTT topic.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromMqttTopicAttribute : Attribute
{
    /// <param name="wildcardIndex">1-based index of the <c>+</c> wildcard to bind.</param>
    public FromMqttTopicAttribute(int wildcardIndex = 1)
    {
        WildcardIndex = wildcardIndex > 0 ? wildcardIndex : 1;
    }

    /// <summary>1-based index of the wildcard segment in the topic template.</summary>
    public int WildcardIndex { get; }
}
