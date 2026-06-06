using System.Collections;
using MQTTnet.Server;

namespace MqttControllerFramework.Pipeline;

/// <summary>
///     Context object passed through the MQTT middleware pipeline for each incoming publish.
///     Carries both the raw MQTTnet event args and the per-message DI scope.
/// </summary>
public sealed class MqttMessageContext
{
    /// <summary>The raw MQTTnet publish event args.</summary>
    public required InterceptingPublishEventArgs Args { get; init; }

    /// <summary>The per-message DI service scope.</summary>
    public required IServiceProvider Services { get; init; }

    // ── Convenience accessors ──────────────────────────────────────────────

    /// <summary>Authenticated username of the publishing client.</summary>
    public string UserName => Args.UserName;

    /// <summary>MQTT topic of the published message.</summary>
    public string Topic => Args.ApplicationMessage.Topic;

    /// <summary>MQTT client identifier.</summary>
    public string ClientId => Args.ClientId;

    /// <summary>Cancellation token from MQTTnet.</summary>
    public CancellationToken CancellationToken => Args.CancellationToken;

    // ── State bags ─────────────────────────────────────────────────────────

    /// <summary>
    ///     Connection-scoped state — persists across all messages for this TCP connection.
    ///     Populated during <c>ValidatingConnectionAsync</c> (e.g. via <c>IMqttConnectionValidator</c>).
    ///     Ideal for storing <c>tenantId</c>, user roles, or other connection-level metadata
    ///     so middleware can read it without re-querying per message.
    /// </summary>
    /// <example>
    /// // During connection validation:
    /// ctx.SessionItems["tenantId"] = await ResolveTenantAsync(ctx.UserName);
    ///
    /// // In tenant middleware:
    /// var tid = context.SessionItems["tenantId"] as string;
    /// if (tid != null) tenantContext.SetTenant(tid);
    /// </example>
    // MQTTnet exposes SessionItems as the non-generic IDictionary
    public IDictionary SessionItems => Args.SessionItems;

    /// <summary>
    ///     Per-message state bag — lives only within this pipeline invocation.
    ///     Use it to pass data between middleware stages without polluting <see cref="SessionItems"/>.
    /// </summary>
    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);
}
