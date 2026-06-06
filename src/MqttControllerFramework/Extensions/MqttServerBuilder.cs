using Microsoft.Extensions.DependencyInjection;
using MqttControllerFramework.Authentication;
using MqttControllerFramework.Authorization;
using MqttControllerFramework.ClientActions;
using MqttControllerFramework.Connection;
using MqttControllerFramework.Events;
using MqttControllerFramework.Middleware;
using MqttControllerFramework.Pipeline;
using MqttControllerFramework.RateLimiting;
using MqttControllerFramework.RetainedMessages;

namespace MqttControllerFramework.Extensions;

/// <summary>
///     Fluent builder returned by <see cref="MqttServerServiceCollectionExtensions.AddMqttServer"/>.
///     Use the <c>With*</c> methods to register consumer-provided implementations
///     for authentication, authorization, connection validation, and controller routing.
/// </summary>
public sealed class MqttServerBuilder(IServiceCollection services)
{
    /// <summary>The underlying service collection.</summary>
    public IServiceCollection Services { get; } = services;

    // ── Controller routing ─────────────────────────────────────────────────

    /// <summary>
    ///     Registers MQTT controllers using the source-generated
    ///     <typeparamref name="TRegistration"/> class.
    /// </summary>
    public MqttServerBuilder WithControllers<TRegistration>(
        Action<System.Text.Json.JsonSerializerOptions>? configureJson = null)
        where TRegistration : IMqttControllerRegistration, new()
    {
        Services.AddMqttControllers<TRegistration>(configureJson);
        return this;
    }

    // ── Middleware pipeline ────────────────────────────────────────────────

    /// <summary>
    ///     Adds a middleware to the MQTT message pipeline.
    ///     Middleware runs in registration order, before the controller dispatcher.
    ///     Use middleware for cross-cutting concerns such as:
    ///     <list type="bullet">
    ///         <item>Multi-tenant resolution (read <c>SessionItems["tenantId"]</c> → call <c>ITenantContext.SetTenant()</c>)</item>
    ///         <item>Structured logging context</item>
    ///         <item>Message transformation or filtering</item>
    ///     </list>
    ///     <para>
    ///         <typeparamref name="TMiddleware"/> is resolved from the per-message DI scope,
    ///         so it can inject scoped or singleton services.
    ///     </para>
    /// </summary>
    /// <example>
    /// // Register:
    /// builder.UseMiddleware&lt;TenantMiddleware&gt;();
    ///
    /// // Implement:
    /// public class TenantMiddleware(ITenantContext tenant) : IMqttMiddleware
    /// {
    ///     public Task InvokeAsync(MqttMessageContext ctx, MqttRequestDelegate next)
    ///     {
    ///         if (ctx.SessionItems.TryGetValue("tenantId", out var tid))
    ///             tenant.SetTenant((string)tid!);
    ///         return next(ctx);
    ///     }
    /// }
    /// </example>
    public MqttServerBuilder UseMiddleware<TMiddleware>()
        where TMiddleware : class, IMqttMiddleware
    {
        Services.AddScoped<IMqttMiddleware, TMiddleware>();
        return this;
    }

    // ── Rate limiting ──────────────────────────────────────────────────────

    /// <summary>Enables the built-in token-bucket rate limiter.</summary>
    public MqttServerBuilder WithRateLimiting()
    {
        Services.AddMqttRateLimiting();
        return this;
    }

    // ── Authentication ─────────────────────────────────────────────────────

    /// <summary>
    ///     Registers a scoped <typeparamref name="TProvider"/> as <see cref="IMqttAuthenticationProvider"/>.
    /// </summary>
    public MqttServerBuilder WithAuthentication<TProvider>()
        where TProvider : class, IMqttAuthenticationProvider
    {
        Services.AddScoped<IMqttAuthenticationProvider, TProvider>();
        return this;
    }

    // ── Authorization ──────────────────────────────────────────────────────

    /// <summary>
    ///     Registers a scoped <typeparamref name="TProvider"/> as <see cref="IMqttAuthorizationProvider"/>.
    ///     If omitted the broker skips publish/subscribe authorization entirely.
    /// </summary>
    public MqttServerBuilder WithAuthorization<TProvider>()
        where TProvider : class, IMqttAuthorizationProvider
    {
        Services.AddScoped<IMqttAuthorizationProvider, TProvider>();
        return this;
    }

    // ── Connection validation ──────────────────────────────────────────────

    /// <summary>
    ///     Registers a scoped <typeparamref name="TValidator"/> as <see cref="IMqttConnectionValidator"/>
    ///     (called before authentication; useful for ClientId format checks, IP bans, etc.).
    /// </summary>
    public MqttServerBuilder WithConnectionValidator<TValidator>()
        where TValidator : class, IMqttConnectionValidator
    {
        Services.AddScoped<IMqttConnectionValidator, TValidator>();
        return this;
    }

    // ── Per-request initializer ────────────────────────────────────────────

    /// <summary>
    ///     Registers a scoped <typeparamref name="TInitializer"/> as <see cref="IMqttRequestInitializer"/>
    ///     (called before each publish dispatch; useful for tenant resolution, logging context, etc.).
    /// </summary>
    public MqttServerBuilder WithRequestInitializer<TInitializer>()
        where TInitializer : class, IMqttRequestInitializer
    {
        Services.AddScoped<IMqttRequestInitializer, TInitializer>();
        return this;
    }

    // ── Network tracker ────────────────────────────────────────────────────

    /// <summary>
    ///     Replaces the built-in in-memory network tracker with a custom
    ///     <typeparamref name="TTracker"/> implementation.
    /// </summary>
    public MqttServerBuilder WithNetworkTracker<TTracker>()
        where TTracker : class, IMqttClientNetworkTracker
    {
        Services.AddSingleton<IMqttClientNetworkTracker, TTracker>();
        return this;
    }

    // ── Retained message storage ───────────────────────────────────────────

    /// <summary>
    ///     Replaces the built-in file-based retain storage with a custom
    ///     <typeparamref name="TStorage"/> implementation (e.g. database, Redis, blob).
    /// </summary>
    public MqttServerBuilder WithRetainStorage<TStorage>()
        where TStorage : class, IRetainStorage
    {
        Services.AddSingleton<IRetainStorage, TStorage>();
        return this;
    }

    // ── Event handlers ─────────────────────────────────────────────────────

    /// <summary>Registers a scoped handler for client connected events.</summary>
    public MqttServerBuilder OnClientConnected<THandler>()
        where THandler : class, IMqttClientConnectedEvent
    {
        Services.AddScoped<IMqttClientConnectedEvent, THandler>();
        return this;
    }

    /// <summary>Registers a scoped handler for client disconnected events.</summary>
    public MqttServerBuilder OnClientDisconnected<THandler>()
        where THandler : class, IMqttClientDisconnectedEvent
    {
        Services.AddScoped<IMqttClientDisconnectedEvent, THandler>();
        return this;
    }

    /// <summary>Registers a scoped handler for client subscribed-topic events.</summary>
    public MqttServerBuilder OnClientSubscribedTopic<THandler>()
        where THandler : class, IMqttClientSubscribedTopicEvent
    {
        Services.AddScoped<IMqttClientSubscribedTopicEvent, THandler>();
        return this;
    }

    /// <summary>Registers a scoped handler for client unsubscribed-topic events.</summary>
    public MqttServerBuilder OnClientUnsubscribedTopic<THandler>()
        where THandler : class, IMqttClientUnsubscribedTopicEvent
    {
        Services.AddScoped<IMqttClientUnsubscribedTopicEvent, THandler>();
        return this;
    }
}
