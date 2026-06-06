namespace MqttControllerFramework.Events;

/// <summary>
///     Register implementations of this interface to be notified when an MQTT client disconnects.
///     Multiple handlers can be registered; all are invoked sequentially.
/// </summary>
public interface IMqttClientDisconnectedEvent
{
    /// <summary>Called after a client disconnects from the broker.</summary>
    Task OnClientDisconnectedAsync(string clientId);
}
