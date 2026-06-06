using System.Net;

namespace MqttControllerFramework.Connection;

/// <summary>
///     Optional service for tracking the remote endpoint of connected MQTT clients.
///     The framework registers a built-in in-memory implementation automatically.
///     Replace it by calling <c>services.AddSingleton&lt;IMqttClientNetworkTracker, YourImpl&gt;()</c>
///     after <c>AddMqttServer()</c>.
/// </summary>
public interface IMqttClientNetworkTracker
{
    /// <summary>Records or updates the endpoint for a client.</summary>
    void TrackClientNetworkActivity(string clientId, EndPoint endPoint);

    /// <summary>Returns the last known IP address for a client, or <c>null</c> if not tracked.</summary>
    string? GetClientEndPointAddress(string clientId);
}
