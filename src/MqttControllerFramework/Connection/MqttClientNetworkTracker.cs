using System.Collections.Concurrent;
using System.Net;

namespace MqttControllerFramework.Connection;

/// <summary>Default in-memory implementation of <see cref="IMqttClientNetworkTracker"/>.</summary>
public sealed class MqttClientNetworkTracker : IMqttClientNetworkTracker
{
    private readonly ConcurrentDictionary<string, string> _map = new();

    /// <inheritdoc/>
    public void TrackClientNetworkActivity(string clientId, EndPoint endPoint)
    {
        _map[clientId] = endPoint.ToString()?.Split(':')[0] ?? string.Empty;
    }

    /// <inheritdoc/>
    public string? GetClientEndPointAddress(string clientId)
    {
        _map.TryGetValue(clientId, out var address);
        return address;
    }
}
