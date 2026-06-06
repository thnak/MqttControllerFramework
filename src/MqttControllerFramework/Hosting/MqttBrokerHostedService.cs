using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using MQTTnet.Server;
using MqttControllerFramework.Authentication;
using MqttControllerFramework.Authorization;
using MqttControllerFramework.Configuration;
using MqttControllerFramework.Connection;
using MqttControllerFramework.Events;
using MqttControllerFramework.Pipeline;
using MqttControllerFramework.RateLimiting;
using MqttControllerFramework.Routing;
using MqttControllerFramework.Stats;

namespace MqttControllerFramework.Hosting;

/// <summary>
///     Hosted service that wires all MQTTnet broker events to the framework's
///     authentication, authorization, rate-limiting, and controller-dispatch pipeline.
/// </summary>
public sealed partial class MqttBrokerHostedService : IHostedService
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger<MqttBrokerHostedService> _logger;
    private readonly MqttServer _broker;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMqttBrokerStatsService _stats;
    private readonly IMqttRateLimitService _rateLimit;
    private readonly IMqttRoutingService _routing;
    private readonly IMqttClientNetworkTracker _networkTracker;
    private readonly MqttServerSettings _settings;
    private readonly ReadOnlyMemory<byte> _systemName;

    public MqttBrokerHostedService(
        ILogger<MqttBrokerHostedService> logger,
        IServiceScopeFactory scopeFactory,
        MqttServer broker,
        IMqttBrokerStatsService stats,
        IMqttRateLimitService rateLimit,
        IMqttRoutingService routing,
        IMqttClientNetworkTracker networkTracker,
        IOptions<MqttServerSettings> settings)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _broker = broker;
        _stats = stats;
        _rateLimit = rateLimit;
        _routing = routing;
        _networkTracker = networkTracker;
        _settings = settings.Value;
        _systemName = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(_settings.ServerOriginPropertyValue));
        WireEvents();
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken stoppingToken)
    {
        await _cts.CancelAsync();
        var clients = await _broker.GetClientsAsync();
        foreach (var client in clients)
            await client.DisconnectAsync(new MqttServerClientDisconnectOptions
            {
                ReasonCode = MqttDisconnectReasonCode.ServerShuttingDown,
                ReasonString = "Server is shutting down."
            });
        await _broker.StopAsync();
    }

    private void WireEvents()
    {
        _broker.ValidatingConnectionAsync        += e => Safe(OnValidatingConnectionAsync, e);
        _broker.StartedAsync                     += e => Safe(OnStartedAsync, e);
        _broker.StoppedAsync                     += e => Safe(OnStoppedAsync, e);
        _broker.ClientConnectedAsync             += e => Safe(OnClientConnectedAsync, e);
        _broker.ClientDisconnectedAsync          += e => Safe(OnClientDisconnectedAsync, e);
        _broker.InterceptingSubscriptionAsync    += e => Safe(OnInterceptingSubscriptionAsync, e);
        _broker.InterceptingUnsubscriptionAsync  += e => Safe(OnInterceptingUnsubscriptionAsync, e);
        _broker.InterceptingPublishAsync         += e => Safe(OnInterceptingPublishAsync, e);
        _broker.PreparingSessionAsync            += e => Safe(OnPreparingSessionAsync, e);
        _broker.RetainedMessageChangedAsync      += e => Safe(OnRetainedMessageChangedAsync, e);
        _broker.RetainedMessagesClearedAsync     += e => Safe(OnRetainedMessagesClearedAsync, e);
        _broker.LoadingRetainedMessageAsync      += e => Safe(OnLoadingRetainedMessageAsync, e);
        _broker.SessionDeletedAsync              += e => Safe(OnSessionDeletedAsync, e);
        _broker.ClientSubscribedTopicAsync       += e => Safe(OnClientSubscribedTopicAsync, e);
        _broker.ClientUnsubscribedTopicAsync     += e => Safe(OnClientUnsubscribedTopicAsync, e);
        _broker.ApplicationMessageEnqueuedOrDroppedAsync  += e => Safe(OnMessageEnqueuedOrDroppedAsync, e);
        _broker.ApplicationMessageNotConsumedAsync        += e => Safe(OnMessageNotConsumedAsync, e);
        _broker.ClientAcknowledgedPublishPacketAsync      += e => Safe(OnClientAcknowledgedPublishPacketAsync, e);
        _broker.InterceptingClientEnqueueAsync            += e => Safe(OnInterceptingClientEnqueueAsync, e);
        _broker.InterceptingInboundPacketAsync            += e => Safe(OnInterceptingInboundPacketAsync, e);
        _broker.InterceptingOutboundPacketAsync           += e => Safe(OnInterceptingOutboundPacketAsync, e);
        _broker.QueuedApplicationMessageOverwrittenAsync  += e => Safe(OnQueuedMessageOverwrittenAsync, e);
    }

    private async Task Safe<T>(Func<T, Task> handler, T arg)
    {
        try { await handler(arg); }
        catch (Exception ex) { _logger.LogError(ex, "Unhandled error in MQTT broker event handler"); }
    }

    // ── Connection validation ──────────────────────────────────────────────

    private async Task OnValidatingConnectionAsync(ValidatingConnectionEventArgs ctx)
    {
        if (!_stats.GetAcceptNewConnections())
        {
            ctx.ReasonCode = MqttConnectReasonCode.ServerUnavailable;
            return;
        }

        if (IsServerMessage(ctx.UserProperties))
            return;

        // Optional consumer-supplied connection validator (ClientId format, IP bans, etc.)
        await using var validatorScope = _scopeFactory.CreateAsyncScope();
        var validator = validatorScope.ServiceProvider.GetService<IMqttConnectionValidator>();
        if (validator != null)
        {
            var result = await validator.ValidateAsync(ctx, _cts.Token);
            if (!result.IsValid)
            {
                LogConnectionRejectedByValidator(ctx.ClientId, result.RejectReason ?? "rejected");
                ctx.ReasonCode = result.RejectReasonCode;
                return;
            }
        }

        if (string.IsNullOrEmpty(ctx.UserName))
        {
            ctx.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
            return;
        }

        await using var authScope = _scopeFactory.CreateAsyncScope();
        var authProvider = authScope.ServiceProvider.GetRequiredService<IMqttAuthenticationProvider>();
        var authResult = await authProvider.AuthenticateAsync(ctx.UserName, ctx.Password, _cts.Token);

        if (!authResult.IsAuthenticated)
        {
            ctx.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
            return;
        }

        _networkTracker.TrackClientNetworkActivity(ctx.ClientId, ctx.RemoteEndPoint);
        await _stats.MqttServerOnValidatingConnectionAsync(ctx);
        ctx.ReasonCode = MqttConnectReasonCode.Success;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private Task OnStartedAsync(EventArgs _)
    {
        _stats.MarkBrokerStarted();
        return Task.CompletedTask;
    }

    private static Task OnStoppedAsync(EventArgs _) => Task.CompletedTask;

    private static Task OnPreparingSessionAsync(EventArgs _) => Task.CompletedTask;

    // ── Client connected / disconnected ───────────────────────────────────

    private async Task OnClientConnectedAsync(ClientConnectedEventArgs args)
    {
        if (IsServerMessage(args.UserProperties)) return;
        await _stats.MqttServerOnClientConnectedAsync(args);
        await using var scope = _scopeFactory.CreateAsyncScope();
        foreach (var handler in scope.ServiceProvider.GetServices<IMqttClientConnectedEvent>())
            await handler.OnClientConnectedAsync(args.ClientId);
    }

    private async Task OnClientDisconnectedAsync(ClientDisconnectedEventArgs args)
    {
        if (IsServerMessage(args.UserProperties)) return;
        await _stats.MqttServerOnClientDisconnectedAsync(args);
        await using var scope = _scopeFactory.CreateAsyncScope();
        foreach (var handler in scope.ServiceProvider.GetServices<IMqttClientDisconnectedEvent>())
            await handler.OnClientDisconnectedAsync(args.ClientId);
    }

    // ── Subscription ──────────────────────────────────────────────────────

    private async Task OnInterceptingSubscriptionAsync(InterceptingSubscriptionEventArgs args)
    {
        if (IsServerMessage(args.UserProperties)) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var authz = scope.ServiceProvider.GetService<IMqttAuthorizationProvider>();
        if (authz != null)
        {
            var result = await authz.AuthorizeSubscribeAsync(
                args.UserName, args.TopicFilter.Topic, (int)args.TopicFilter.QualityOfServiceLevel, args.CancellationToken);

            if (!result.IsAuthorized)
            {
                LogSubscriptionDenied(args.ClientId, args.TopicFilter.Topic, result.DenialReason ?? "access denied");
                args.Response.ReasonCode = MqttSubscribeReasonCode.NotAuthorized;
                args.Response.ReasonString = result.DenialReason ?? "Not authorized to subscribe to this topic.";
                return;
            }

            var grantedQos = result.MaxQoS ?? (int)args.TopicFilter.QualityOfServiceLevel;
            args.Response.ReasonCode = grantedQos switch
            {
                0 => MqttSubscribeReasonCode.GrantedQoS0,
                1 => MqttSubscribeReasonCode.GrantedQoS1,
                2 => MqttSubscribeReasonCode.GrantedQoS2,
                _ => MqttSubscribeReasonCode.GrantedQoS0
            };
        }

        await _stats.MqttServerOnInterceptingSubscriptionAsync(args);
    }

    private async Task OnInterceptingUnsubscriptionAsync(InterceptingUnsubscriptionEventArgs args)
    {
        if (IsServerMessage(args.UserProperties)) return;
        await _stats.MqttServerOnInterceptingUnsubscriptionAsync(args);
    }

    // ── Publish ───────────────────────────────────────────────────────────

    private async Task OnInterceptingPublishAsync(InterceptingPublishEventArgs args)
    {
        var isServer = IsServerMessage(args.ApplicationMessage.UserProperties);
        await _stats.MqttServerOnInterceptingPublishAsync(args, isServer);
        if (isServer) return;

        var topic = args.ApplicationMessage.Topic;
        var payloadSize = args.ApplicationMessage.Payload.Length;

        // Rate limiting
        var rateResult = await _rateLimit.CheckRateLimitAsync(args.UserName, topic, payloadSize, args.CancellationToken);
        if (!rateResult.IsAllowed)
        {
            args.ProcessPublish = false;
            LogRateLimitExceeded(args.ClientId, topic, rateResult.DenialReason);
            await _broker.DisconnectClientAsync(args.ClientId, MqttDisconnectReasonCode.MessageRateTooHigh);
            return;
        }

        // One scope for authorization + middleware + controller dispatch
        await using var scope = _scopeFactory.CreateAsyncScope();

        // Authorization (global — before middleware runs)
        var authz = scope.ServiceProvider.GetService<IMqttAuthorizationProvider>();
        if (authz != null)
        {
            var authResult = await authz.AuthorizePublishAsync(
                args.UserName, topic, (int)args.ApplicationMessage.QualityOfServiceLevel,
                args.ApplicationMessage.Retain, args.CancellationToken);

            if (!authResult.IsAuthorized)
            {
                args.ProcessPublish = false;
                args.Response.ReasonCode = MqttPubAckReasonCode.NotAuthorized;
                args.Response.ReasonString = authResult.DenialReason ?? "Not authorized to publish to this topic.";
                LogPublishDenied(args.ClientId, topic, authResult.DenialReason ?? "access denied");
                return;
            }
        }

        // Build context and run middleware → controller pipeline
        var context = new MqttMessageContext { Args = args, Services = scope.ServiceProvider };
        try
        {
            await RunPipelineAsync(context);
            args.Response.ReasonCode = MqttPubAckReasonCode.Success;
        }
        catch (Exception ex)
        {
            LogDispatchError(args.ClientId, topic, ex.Message);
            args.Response.ReasonCode = MqttPubAckReasonCode.UnspecifiedError;
            args.Response.ReasonString = "Internal server error.";
        }
    }

    // ── Topic events ──────────────────────────────────────────────────────

    private async Task OnClientSubscribedTopicAsync(ClientSubscribedTopicEventArgs args)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        foreach (var handler in scope.ServiceProvider.GetServices<IMqttClientSubscribedTopicEvent>())
            await handler.OnClientSubscribedTopicAsync(args.ClientId, args.TopicFilter.Topic);
    }

    private async Task OnClientUnsubscribedTopicAsync(ClientUnsubscribedTopicEventArgs args)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        foreach (var handler in scope.ServiceProvider.GetServices<IMqttClientUnsubscribedTopicEvent>())
            await handler.OnClientUnsubscribedTopicAsync(args.ClientId, args.TopicFilter);
    }

    // ── Retained messages ─────────────────────────────────────────────────

    private async Task OnLoadingRetainedMessageAsync(LoadingRetainedMessagesEventArgs args)
    {
        var path = _settings.RetainedMessagesFilePath;
        if (!File.Exists(path)) return;
        await using var fs = File.OpenRead(path);
        var messages = await JsonSerializer.DeserializeAsync<List<MQTTnet.MqttApplicationMessage>>(fs);
        if (messages != null) args.LoadedRetainedMessages = messages;
    }

    private async Task OnRetainedMessageChangedAsync(RetainedMessageChangedEventArgs args)
    {
        var path = _settings.RetainedMessagesFilePath;
        await using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, args.StoredRetainedMessages);
    }

    private Task OnRetainedMessagesClearedAsync(EventArgs _)
    {
        var path = _settings.RetainedMessagesFilePath;
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    // ── Stats counters ────────────────────────────────────────────────────

    private Task OnSessionDeletedAsync(SessionDeletedEventArgs args) =>
        _stats.MqttServerOnSessionDeletedAsync(args);

    private Task OnMessageEnqueuedOrDroppedAsync(ApplicationMessageEnqueuedEventArgs args)
    {
        if (args.IsDropped)
        {
            _stats.IncrementDroppedMessageCount();
            LogMessageDropped(args.ReceiverClientId);
        }
        return Task.CompletedTask;
    }

    private Task OnMessageNotConsumedAsync(ApplicationMessageNotConsumedEventArgs args)
    {
        _stats.IncrementUnconsumedMessageCount();
        LogMessageNotConsumed(args.SenderId, args.ApplicationMessage.Topic);
        return Task.CompletedTask;
    }

    private Task OnClientAcknowledgedPublishPacketAsync(ClientAcknowledgedPublishPacketEventArgs _)
    {
        _stats.IncrementAcknowledgedMessageCount();
        return Task.CompletedTask;
    }

    private Task OnInterceptingClientEnqueueAsync(InterceptingClientApplicationMessageEnqueueEventArgs args)
    {
        args.AcceptEnqueue = true;
        return Task.CompletedTask;
    }

    private Task OnInterceptingInboundPacketAsync(InterceptingPacketEventArgs args)
    {
        args.ProcessPacket = true;
        return Task.CompletedTask;
    }

    private Task OnInterceptingOutboundPacketAsync(InterceptingPacketEventArgs args)
    {
        args.ProcessPacket = true;
        return Task.CompletedTask;
    }

    private Task OnQueuedMessageOverwrittenAsync(QueueMessageOverwrittenEventArgs args)
    {
        _stats.IncrementQueueOverwriteCount();
        LogQueueOverwritten(args.ReceiverClientId);
        return Task.CompletedTask;
    }

    // ── Middleware pipeline ───────────────────────────────────────────────

    private Task RunPipelineAsync(MqttMessageContext context)
    {
        // Resolve all middleware registered in the per-message scope (registration order = execution order)
        var middlewares = context.Services.GetServices<IMqttMiddleware>();

        // Build chain right-to-left so first registered runs first
        var pipeline = middlewares
            .Reverse()
            .Aggregate((MqttRequestDelegate)Terminal, static (next, mw) => ctx => mw.InvokeAsync(ctx, next));

        return pipeline(context);

        // Terminal step: controller dispatch
        Task Terminal(MqttMessageContext ctx) => _routing.RouteAsync(ctx);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private bool IsServerMessage(List<MqttUserProperty>? properties)
    {
        var prop = properties?.FirstOrDefault(p => p.Name == _settings.ServerOriginPropertyName);
        return prop != null && prop.ValueBuffer.Span.SequenceEqual(_systemName.Span);
    }

    // ── Log helpers ───────────────────────────────────────────────────────

    [LoggerMessage(LogLevel.Warning, "Connection rejected by validator for client {clientId}: {reason}")]
    partial void LogConnectionRejectedByValidator(string clientId, string reason);

    [LoggerMessage(LogLevel.Warning, "Publish denied for client {clientId} on topic {topic}: {reason}")]
    partial void LogPublishDenied(string clientId, string topic, string reason);

    [LoggerMessage(LogLevel.Warning, "Subscription denied for client {clientId} on topic {topic}: {reason}")]
    partial void LogSubscriptionDenied(string clientId, string topic, string reason);

    [LoggerMessage(LogLevel.Warning, "Rate limit exceeded for client {clientId} on topic {topic}: {reason}")]
    partial void LogRateLimitExceeded(string clientId, string topic, string? reason);

    [LoggerMessage(LogLevel.Error, "Error dispatching message from client {clientId} on topic {topic}: {message}")]
    partial void LogDispatchError(string clientId, string topic, string message);

    [LoggerMessage(LogLevel.Debug, "Message dropped for receiver {clientId}")]
    partial void LogMessageDropped(string clientId);

    [LoggerMessage(LogLevel.Debug, "Message from {senderId} on topic {topic} not consumed")]
    partial void LogMessageNotConsumed(string senderId, string topic);

    [LoggerMessage(LogLevel.Debug, "Queued message overwritten for client {clientId}")]
    partial void LogQueueOverwritten(string clientId);
}
