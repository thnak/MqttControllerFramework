# MqttControllerFramework

A source-generated MQTT controller framework for .NET, built on top of [MQTTnet](https://github.com/dotnet/MQTTnet).

## Features

- **Attribute-driven routing** — decorate methods with `[MqttRoute("topic/+")]` and the source generator wires them up automatically
- **Middleware pipeline** — `IMqttMiddleware` / `MqttRequestDelegate` (ASP.NET Core–style), with per-message DI scope and connection-scoped `SessionItems` for multi-tenancy
- **Authentication / Authorization** — plug in `IMqttAuthenticationProvider` and `IMqttAuthorizationProvider` without touching domain types
- **Connection validation** — optional `IMqttConnectionValidator` hook (ClientId format checks, IP bans, etc.)
- **Token-bucket rate limiting** — per-client / per-topic, configurable via `[TokenBucketRateLimit]` attribute
- **Built-in stats** — `IMqttBrokerStatsService` exposes message counters, byte throughput, and per-topic summaries
- **TLS hot-swap** — `HotSwappableServerCertProvider` reloads PEM / PKCS#12 certificates without restart
- **Multi-target** — `net8.0` and `net10.0`

## Quick Start

```csharp
// Program.cs
builder.Services
    .AddMqttServer(builder.Configuration)
    .WithControllers<GeneratedMqttControllerRegistration>()
    .WithAuthentication<MyAuthProvider>()
    .WithAuthorization<MyAuthzProvider>()
    .UseMiddleware<TenantMiddleware>()
    .WithRateLimiting();
```

```csharp
// appsettings.json
"MqttSettings": {
  "EnableNonSsl": true,
  "NonSslPort": 1883,
  "EnableSsl": false
}
```

```csharp
// Controller
[MqttController]
public class SensorsController
{
    [MqttRoute("sensors/{deviceId}/temperature")]
    public async Task OnTemperature(string deviceId, TemperaturePayload payload)
    {
        // handle message
    }
}
```

## Middleware Example (multi-tenancy)

```csharp
public class TenantMiddleware(ITenantContext tenant) : IMqttMiddleware
{
    public Task InvokeAsync(MqttMessageContext ctx, MqttRequestDelegate next)
    {
        if (ctx.SessionItems["tenantId"] is string tid)
            tenant.SetTenant(tid);
        return next(ctx);
    }
}
```

`SessionItems` is populated once during `IMqttConnectionValidator.ValidateAsync` and persists for the lifetime of the TCP connection, making it ideal for tenant or user resolution.

## Status

> **Alpha** — API may change before 1.0. Not recommended for production.

## License

MIT
