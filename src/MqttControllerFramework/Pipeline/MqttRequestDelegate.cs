namespace MqttControllerFramework.Pipeline;

/// <summary>
///     Represents the next step in the MQTT middleware pipeline.
///     Invoke it to pass control to the next middleware, or skip it to short-circuit.
/// </summary>
public delegate Task MqttRequestDelegate(MqttMessageContext context);
