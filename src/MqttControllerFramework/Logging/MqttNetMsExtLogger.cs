using Microsoft.Extensions.Logging;
using MQTTnet.Diagnostics.Logger;

namespace MqttControllerFramework.Logging;

/// <summary>Bridges MQTTnet's internal logging to <see cref="ILogger"/>.</summary>
public sealed class MqttNetMsExtLogger(ILogger<MqttNetMsExtLogger> logger) : IMqttNetLogger
{
    /// <inheritdoc/>
    public void Publish(MqttNetLogLevel level, string source, string message, object[] parameters, Exception exception)
    {
        var logLevel = level switch
        {
            MqttNetLogLevel.Verbose => LogLevel.Trace,
            MqttNetLogLevel.Info    => LogLevel.Information,
            MqttNetLogLevel.Warning => LogLevel.Warning,
            MqttNetLogLevel.Error   => LogLevel.Error,
            _                       => LogLevel.None
        };
        logger.Log(logLevel, exception, "[{Source}] {Message}", source, message);
    }

    /// <inheritdoc/>
    public bool IsEnabled => true;
}
