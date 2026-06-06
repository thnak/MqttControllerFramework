using Microsoft.Extensions.DependencyInjection;
using MqttControllerFramework.Authentication;
using MqttControllerFramework.Authorization;
using MqttControllerFramework.ClientActions;
using MqttControllerFramework.Connection;
using MqttControllerFramework.Events;
using MqttControllerFramework.Multitenancy;
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

    // ── Multi-tenancy ──────────────────────────────────────────────────────

    /// <summary>
    ///     Registers <typeparamref name="TResolver"/> as the tenant resolver and wires up
    ///     the built-in multi-tenancy support:
    ///     <list type="bullet">
    ///         <item><typeparamref name="TResolver"/> is called after every successful authentication
    ///               and stores the resolved ID in <c>SessionItems["tenantId"]</c>.</item>
    ///         <item><see cref="ITenantContext"/> is registered as scoped and populated per-message
    ///               by the auto-added <see cref="TenantMiddleware"/>.</item>
    ///         <item>Inject <see cref="ITenantContext"/> into controllers or any scoped service
    ///               to read <see cref="ITenantContext.TenantId"/>.</item>
    ///     </list>
    ///     Call this <em>before</em> other <c>UseMiddleware</c> calls so tenant context
    ///     is set first in the pipeline.
    /// </summary>
    public MqttServerBuilder WithTenantResolver<TResolver>()
        where TResolver : class, IMqttTenantResolver
    {
        Services.AddScoped<IMqttTenantResolver, TResolver>();
        Services.AddScoped<ITenantContext, TenantContext>();
        Services.AddScoped<IMqttMiddleware, TenantMiddleware>();
        return this;
    }

    /// <summary>
    ///     Typed overload — registers <typeparamref name="TResolver"/> and wires the full
    ///     tenant object into a per-message <see cref="ITenantContext{TTenant}"/>.
    ///     <para>
    ///         The resolver runs once per TCP connection (after successful authentication)
    ///         and stores the resolved <typeparamref name="TTenant"/> object in
    ///         <c>SessionItems[<see cref="MqttTenantConstants.TenantInfoKey"/>]</c>.
    ///         <see cref="TenantMiddleware{T}"/> then populates
    ///         <see cref="ITenantContext{T}"/> for every message in that connection's scope.
    ///     </para>
    ///     <para>
    ///         Inject <see cref="ITenantContext{TTenant}"/> into controllers to access
    ///         <c>Tenant</c>. To integrate with Finbuckle, add a thin bridge middleware:
    ///     </para>
    ///     <code>
    ///     public class FinbuckleBridge(
    ///         ITenantContext&lt;AppTenantInfo&gt; ctx,
    ///         IMultiTenantContextSetter setter) : IMqttMiddleware
    ///     {
    ///         public Task InvokeAsync(MqttMessageContext msg, MqttRequestDelegate next)
    ///         {
    ///             if (ctx.Tenant is { } info)
    ///                 setter.MultiTenantContext = new MultiTenantContext&lt;AppTenantInfo&gt; { TenantInfo = info };
    ///             return next(msg);
    ///         }
    ///     }
    ///     </code>
    /// </summary>
    public MqttServerBuilder WithTenantResolver<TResolver, TTenant>()
        where TResolver : class, IMqttTenantResolver<TTenant>
        where TTenant : class
    {
        Services.AddScoped<IMqttTenantResolver<TTenant>, TResolver>();
        Services.AddScoped<IMqttTenantResolverRunner, MqttTenantResolverRunner<TTenant>>();
        Services.AddScoped<ITenantContext<TTenant>, TenantContext<TTenant>>();
        Services.AddScoped<IMqttMiddleware, TenantMiddleware<TTenant>>();
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
