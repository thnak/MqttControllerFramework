namespace MqttControllerFramework.Events;

/// <summary>
///     Register implementations of this interface to be notified when an MQTT client connects.
///     Multiple handlers can be registered; all are invoked sequentially.
/// </summary>
public interface IMqttClientConnectedEvent
{
    /// <summary>Called after a client successfully connects to the broker.</summary>
    Task OnClientConnectedAsync(string clientId);
}
