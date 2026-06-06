using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MqttControllerFramework.Routing;

/// <summary>
///     Holds the compiled route table built from the source-generated route list.
/// </summary>
public sealed class MqttRouter
{
    private readonly ILogger<MqttRouter> _logger;

    /// <param name="logger">Logger instance.</param>
    /// <param name="routes">Pre-generated routes from the source generator.</param>
    public MqttRouter(ILogger<MqttRouter> logger, IReadOnlyList<MqttRoute> routes)
    {
        _logger = logger;
        LoadRoutes(routes);
    }

    /// <summary>Topic template → route mapping.</summary>
    public ConcurrentDictionary<string, MqttRoute> Routes { get; } = new();

    private void LoadRoutes(IReadOnlyList<MqttRoute> routes)
    {
        _logger.LogInformation("Loading {Count} MQTT routes from source generator.", routes.Count);

        foreach (var route in routes)
            if (Routes.TryAdd(route.TopicTemplate, route))
                _logger.LogDebug("Mapped topic '{Template}' → {Controller}.{Method}",
                    route.TopicTemplate, route.ControllerType.Name, route.HandlerMethod.Name);
    }
}
