# Middleware

## Overview

MqttControllerFramework has an ASP.NET Core–style middleware pipeline. Every incoming publish passes through all registered middleware in registration order before reaching the controller dispatcher.

```
MQTT Publish
     │
     ▼
[Middleware 1]  ─→  [Middleware 2]  ─→  ...  ─→  [Controller Dispatcher]
```

Each middleware decides whether to call `next(ctx)` to continue the chain or short-circuit it (e.g. drop a message or send a response without invoking the controller).

---

## `IMqttMiddleware`

```csharp
public interface IMqttMiddleware
{
    Task InvokeAsync(MqttMessageContext ctx, MqttRequestDelegate next);
}
```

Call `next(ctx)` to pass execution to the next stage; skip it to short-circuit.

---

## `MqttMessageContext`

The context object passed through every stage:

```csharp
public sealed class MqttMessageContext
{
    // Raw MQTTnet event args — full control over ProcessPublish, Response, etc.
    public InterceptingPublishEventArgs Args { get; }

    // Per-message DI scope — resolve scoped services
    public IServiceProvider Services { get; }

    // Convenience accessors
    public string UserName { get; }    // publishing client's username
    public string Topic { get; }       // MQTT topic
    public string ClientId { get; }    // MQTT client identifier
    public CancellationToken CancellationToken { get; }

    // State bags
    public IDictionary SessionItems { get; }       // connection-scoped (see below)
    public IDictionary<string, object?> Items { get; } // per-message
}
```

### `SessionItems` — connection-scoped state

`SessionItems` is backed by MQTTnet's own session dictionary and **persists for the lifetime of the TCP connection**. Populate it once in `IMqttConnectionValidator.ValidateAsync`; read it in every subsequent middleware or controller for that client.

```csharp
// In IMqttConnectionValidator:
ctx.SessionItems["tenantId"] = await tenantStore.ResolveAsync(ctx.UserName);
ctx.SessionItems["roles"]    = await authStore.GetRolesAsync(ctx.UserName);

// In middleware:
var tenantId = context.SessionItems["tenantId"] as string;
```

This avoids a database round-trip on every message.

### `Items` — per-message state

`Items` is a fresh `Dictionary<string, object?>` for each publish. Use it to pass data between middleware stages without touching `SessionItems`.

```csharp
public class LoggingMiddleware : IMqttMiddleware
{
    public async Task InvokeAsync(MqttMessageContext ctx, MqttRequestDelegate next)
    {
        var sw = Stopwatch.StartNew();
        ctx.Items["requestStart"] = sw;
        await next(ctx);
        sw.Stop();
        Console.WriteLine($"{ctx.Topic} handled in {sw.ElapsedMilliseconds} ms");
    }
}
```

---

## Registering Middleware

Call `UseMiddleware<T>()` on the builder. Multiple calls register multiple middleware in order:

```csharp
builder.Services
    .AddMqttServer(configuration)
    .WithControllers<GeneratedMqttControllerRegistration>()
    .UseMiddleware<TenantMiddleware>()      // runs first
    .UseMiddleware<LoggingMiddleware>()     // runs second
    .UseMiddleware<RateLimitMiddleware>()   // runs third
    .WithAuthentication<MyAuthProvider>();
```

Middleware is resolved from the **per-message DI scope**, so it can inject scoped services.

---

## Multi-Tenancy Example

A typical multi-tenant setup:

```csharp
// 1. Seed the tenant into SessionItems at connection time
public class ConnectionValidator(ITenantStore store) : IMqttConnectionValidator
{
    public async ValueTask<MqttConnectionValidationResult> ValidateAsync(
        ValidatingConnectionEventArgs ctx, CancellationToken ct)
    {
        var tenant = await store.FindByUsernameAsync(ctx.UserName, ct);
        if (tenant is null)
            return MqttConnectionValidationResult.Reject("Unknown tenant");

        ctx.SessionItems["tenantId"] = tenant.Id;
        return MqttConnectionValidationResult.Accept();
    }
}

// 2. Apply the tenant on every message
public class TenantMiddleware(ITenantContext tenantCtx) : IMqttMiddleware
{
    public Task InvokeAsync(MqttMessageContext ctx, MqttRequestDelegate next)
    {
        if (ctx.SessionItems["tenantId"] is string tid)
            tenantCtx.SetTenant(tid);
        return next(ctx);
    }
}
```

---

## Short-Circuiting

Skip `next(ctx)` to prevent the message from reaching the controller:

```csharp
public class MaintenanceModeMiddleware(IMaintenanceFlag flag) : IMqttMiddleware
{
    public Task InvokeAsync(MqttMessageContext ctx, MqttRequestDelegate next)
    {
        if (flag.IsEnabled)
        {
            ctx.Args.ProcessPublish = false;
            return Task.CompletedTask; // short-circuit
        }
        return next(ctx);
    }
}
```

---

## Resolving Services in Middleware

`ctx.Services` exposes the per-message DI scope:

```csharp
public class AuditMiddleware : IMqttMiddleware
{
    public async Task InvokeAsync(MqttMessageContext ctx, MqttRequestDelegate next)
    {
        var audit = ctx.Services.GetRequiredService<IAuditService>();
        await audit.LogAsync(ctx.ClientId, ctx.Topic);
        await next(ctx);
    }
}
```
