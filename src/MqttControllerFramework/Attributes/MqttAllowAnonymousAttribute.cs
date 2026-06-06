namespace MqttControllerFramework.Attributes;

/// <summary>
///     Overrides any <see cref="MqttAuthorizeAttribute"/> applied at the class level,
///     allowing anonymous access to this specific handler.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class MqttAllowAnonymousAttribute : Attribute;
