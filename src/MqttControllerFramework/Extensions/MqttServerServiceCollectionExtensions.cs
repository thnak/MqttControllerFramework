using System.Net;
using System.Security.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MQTTnet.AspNetCore;
using MQTTnet.Diagnostics.Logger;
using MqttControllerFramework.ClientActions;
using MqttControllerFramework.Configuration;
using MqttControllerFramework.Connection;
using MqttControllerFramework.Hosting;
using MqttControllerFramework.Logging;
using MqttControllerFramework.RetainedMessages;
using MqttControllerFramework.Security;
using MqttControllerFramework.Stats;

namespace MqttControllerFramework.Extensions;

/// <summary>
///     Entry-point extension methods for adding the MQTT broker server to the DI container.
/// </summary>
public static class MqttServerServiceCollectionExtensions
{
    /// <summary>
    ///     Registers all framework services (MQTTnet server, stats, hosted service, etc.)
    ///     and returns an <see cref="MqttServerBuilder"/> for fluent consumer-side wiring.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">
    ///     Optional configuration section (e.g. <c>config.GetSection("MqttSettings")</c>).
    ///     When <c>null</c>, defaults are used.
    /// </param>
    public static MqttServerBuilder AddMqttServer(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        // Bind settings
        if (configuration != null)
            services.Configure<MqttServerSettings>(configuration);
        else
            services.TryAddSingleton(Options.Create(new MqttServerSettings()));

        // Resolve settings eagerly to configure MQTTnet
        var settings = configuration != null
            ? configuration.Get<MqttServerSettings>() ?? new MqttServerSettings()
            : new MqttServerSettings();

        // TCP + WebSocket adapters
        services.AddMqttTcpServerAdapter();
        services.AddMqttWebSocketServerAdapter();

        // MQTTnet logger bridge
        services.TryAddSingleton<IMqttNetLogger, MqttNetMsExtLogger>();

        // TLS cert provider — constructed eagerly so it can be captured in the MQTTnet config lambda
        var useSsl = settings.EnableSsl && !string.IsNullOrEmpty(settings.PkcsPath);
        HotSwappableServerCertProvider? certProvider = null;
        if (useSsl)
        {
            certProvider = new HotSwappableServerCertProvider(Options.Create(settings));
            services.AddSingleton(certProvider);
            services.AddSingleton<IHotSwappableServerCertProvider>(certProvider);
        }

        // MQTTnet server
        services.AddMqttServer(mqttConfig =>
        {
            mqttConfig
                .WithConnectionBacklog(5)
                .WithPersistentSessions()
                .WithKeepAlive()
                .WithMaxPendingMessagesPerClient(100)
                .WithDefaultCommunicationTimeout(TimeSpan.FromSeconds(60))
                .WithTcpKeepAliveTime(60);

            if (settings.EnableNonSsl)
            {
                mqttConfig
                    .WithDefaultEndpoint()
                    .WithDefaultEndpointPort(settings.NonSslPort)
                    .WithDefaultEndpointBoundIPAddress(IPAddress.Any)
                    .WithDefaultEndpointBoundIPV6Address(IPAddress.IPv6Any);
            }

            if (useSsl && certProvider != null)
            {
                mqttConfig
                    .WithEncryptedEndpoint()
                    .WithEncryptionCertificate(certProvider)
                    .WithClientCertificate(certProvider.RemoteCertificateValidationCallback)
                    .WithEncryptionSslProtocol(SslProtocols.Tls12 | SslProtocols.Tls13)
                    .WithEncryptedEndpointBoundIPAddress(IPAddress.Any)
                    .WithEncryptedEndpointBoundIPV6Address(IPAddress.IPv6Any)
                    .WithEncryptedEndpointPort(settings.SslPort);
            }
        });

        // Framework built-in services
        services.TryAddSingleton<IMqttBrokerStatsService, MqttBrokerStatsService>();
        services.TryAddSingleton<IMqttClientNetworkTracker, MqttClientNetworkTracker>();
        services.TryAddSingleton<IMqttClientActionService, MqttClientActionService>();
        services.TryAddSingleton<IRetainStorage, FileRetainStorage>();

        // Hosted service that wires all broker events
        services.AddHostedService<MqttBrokerHostedService>();

        return new MqttServerBuilder(services);
    }
}
