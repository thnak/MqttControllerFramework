# MqttControllerFramework

[![NuGet](https://img.shields.io/nuget/v/MqttControllerFramework.svg)](https://www.nuget.org/packages/MqttControllerFramework)
[![NuGet Downloads](https://img.shields.io/nuget/dt/MqttControllerFramework.svg)](https://www.nuget.org/packages/MqttControllerFramework)
[![.NET](https://img.shields.io/badge/.NET-8%20%7C%2010-512BD4)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

An ASP.NET Core–style **source-generated MQTT controller framework** for .NET, built on top of [MQTTnet 5](https://github.com/dotnet/MQTTnet). Define topic handlers with attributes; the compile-time source generator handles routing, dispatch, and DI wiring — zero reflection at runtime.

---

## Features

| Feature | Description |
|---|---|
| **Attribute-driven routing** | `[MqttController]` + `[MqttTopic("sensors/+/temperature")]` |
| **Source-generated dispatch** | Routing, dispatcher, and registration code generated at compile time |
| **Compile-time route validation** | MQTT001/MQTT002 diagnostics flag duplicate and ambiguous topic patterns at build time |
| **Middleware pipeline** | ASP.NET Core–style `IMqttMiddleware` with per-message DI scope |
| **Authentication** | `IMqttAuthenticationProvider` — username/password with lockout support |
| **Authorization** | `IMqttAuthorizationProvider` — publish + subscribe, QoS and retain constraints |
| **Connection validation** | `IMqttConnectionValidator` — pre-auth ClientId/IP checks, session-item seeding, auth bypass |
| **Token-bucket rate limiting** | `[TokenBucketRateLimit(capacity, refillRate, intervalMs)]` per-route |
| **Broker stats** | `IMqttBrokerStatsService` — message counters, byte throughput, per-topic summaries |
| **TLS hot-swap** | Reload PEM / PKCS#12 certificates at runtime without restart |
| **Server-side publish** | `IMqttClientActionService` — push messages from broker to topics |
| **Custom payload parsers** | `IMqttPayloadParser<T>` — binary, MessagePack, Protobuf, etc. |
| **Pluggable retain storage** | `IRetainStorage` — swap the built-in JSON file backend for a database, Redis, or blob store |
| **Lifecycle events** | Connected, disconnected, subscribed, unsubscribed |
| **Multi-target** | `net8.0` and `net10.0` |

---

## Installation

```bash
dotnet add package MqttControllerFramework
```

Or add directly to your project file:

```xml
<PackageReference Include="MqttControllerFramework" Version="1.0.0" />
```

---

## Quick Start

### 1. Register the broker

```csharp
// Program.cs
builder.Services
    .AddMqttServer(builder.Configuration.GetSection("MqttSettings"))
    .WithControllers<GeneratedMqttControllerRegistration>()
    .WithAuthentication<MyAuthProvider>();

var app = builder.Build();
app.Run();
```

### 2. Configure

```json
// appsettings.json
{
  "MqttSettings": {
    "EnableNonSsl": true,
    "NonSslPort": 1883
  }
}
```

### 3. Write a controller

```csharp
using MqttControllerFramework.Attributes;

[MqttController]
public class SensorsController
{
    [MqttTopic("sensors/+/temperature")]
    public async Task OnTemperature(
        [FromMqttTopic(1)] string deviceId,
        TemperaturePayload payload,
        CancellationToken ct)
    {
        Console.WriteLine($"Device {deviceId}: {payload.Value}°C");
    }
}

public record TemperaturePayload(double Value, string Unit);
```

### 4. Implement authentication

```csharp
public class MyAuthProvider : IMqttAuthenticationProvider
{
    public ValueTask<MqttAuthenticationResult> AuthenticateAsync(
        string username, string password, CancellationToken ct)
    {
        var ok = username == "device" && password == "secret";
        return ValueTask.FromResult(
            ok ? MqttAuthenticationResult.Authenticated()
               : MqttAuthenticationResult.Failed("Invalid credentials"));
    }
}
```

> The source generator produces `GeneratedMqttControllerRegistration` in the `<YourAssembly>.Mqtt.Generated` namespace at build time.
> Add a `using` for that namespace, or use a global using, to reference the class.

---

## Full Builder API

```csharp
builder.Services
    .AddMqttServer(configuration)            // registers the broker, reads MqttSettings section
    .WithControllers<TRegistration>()        // source-generated registration class
    .WithAuthentication<TProvider>()         // IMqttAuthenticationProvider (required)
    .WithAuthorization<TProvider>()          // IMqttAuthorizationProvider (optional)
    .WithConnectionValidator<TValidator>()   // IMqttConnectionValidator — pre-auth hook
    .WithNetworkTracker<TTracker>()          // replace built-in in-memory tracker
    .WithRetainStorage<TStorage>()           // IRetainStorage — custom retain message backend
    .UseMiddleware<TMiddleware>()            // IMqttMiddleware (ordered, chainable)
    .WithRateLimiting()                      // enable token-bucket rate limiting
    .OnClientConnected<THandler>()           // IMqttClientConnectedEvent
    .OnClientDisconnected<THandler>()        // IMqttClientDisconnectedEvent
    .OnClientSubscribedTopic<THandler>()     // IMqttClientSubscribedTopicEvent
    .OnClientUnsubscribedTopic<THandler>();  // IMqttClientUnsubscribedTopicEvent
```

---

## Middleware Example (multi-tenancy)

`SessionItems` is populated once during connection validation and lives for the lifetime of the TCP connection — ideal for tenant, user, or role resolution:

```csharp
// Connection validator — runs once per connection
public class ConnectionValidator : IMqttConnectionValidator
{
    public async ValueTask<MqttConnectionValidationResult> ValidateAsync(
        ValidatingConnectionEventArgs ctx, CancellationToken ct)
    {
        ctx.SessionItems["tenantId"] = await ResolveTenantAsync(ctx.UserName);
        return MqttConnectionValidationResult.Accept();
    }
}

// Middleware — runs for every message
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

Register both:

```csharp
builder.Services
    .AddMqttServer(configuration)
    .WithControllers<GeneratedMqttControllerRegistration>()
    .WithConnectionValidator<ConnectionValidator>()
    .UseMiddleware<TenantMiddleware>()
    .WithAuthentication<MyAuthProvider>();
```

---

## Rate Limiting

```csharp
[MqttController]
public class TelemetryController
{
    // Allow burst of 10, refill 5 tokens every 1 second, consume 1 per message
    [MqttTopic("telemetry/+/data")]
    [TokenBucketRateLimit(capacity: 10, refillRate: 5, refillIntervalMs: 1000)]
    public Task OnData([FromMqttTopic(1)] string deviceId, SensorData payload) { ... }
}
```

Enable rate limiting in the builder:

```csharp
.WithRateLimiting()
```

---

## TLS

```json
"MqttSettings": {
  "EnableSsl": true,
  "SslPort": 8883,
  "PkcsPath": "/etc/certs/broker.pfx",
  "PkcsPassword": "changeme"
}
```

PEM + separate key file:

```json
"MqttSettings": {
  "EnableSsl": true,
  "SslPort": 8883,
  "PkcsPath": "/etc/certs/broker.crt",
  "PkcsKeyPath": "/etc/certs/broker.key"
}
```

---

## Documentation

| Topic | Page |
|---|---|
| Controllers & routing | [docs/wiki/Controllers-and-Routing.md](docs/wiki/Controllers-and-Routing.md) |
| Middleware pipeline | [docs/wiki/Middleware.md](docs/wiki/Middleware.md) |
| Authentication | [docs/wiki/Authentication.md](docs/wiki/Authentication.md) |
| Authorization | [docs/wiki/Authorization.md](docs/wiki/Authorization.md) |
| Connection validation | [docs/wiki/Connection-Validation.md](docs/wiki/Connection-Validation.md) |
| Rate limiting | [docs/wiki/Rate-Limiting.md](docs/wiki/Rate-Limiting.md) |
| TLS & certificates | [docs/wiki/TLS-and-Security.md](docs/wiki/TLS-and-Security.md) |
| Retained messages | [docs/wiki/Retained-Messages.md](docs/wiki/Retained-Messages.md) |
| Lifecycle events | [docs/wiki/Events.md](docs/wiki/Events.md) |
| Server-side publish | [docs/wiki/Server-Actions.md](docs/wiki/Server-Actions.md) |
| Broker statistics | [docs/wiki/Broker-Stats.md](docs/wiki/Broker-Stats.md) |
| Configuration reference | [docs/wiki/Configuration-Reference.md](docs/wiki/Configuration-Reference.md) |

---

## License

[MIT](LICENSE) © IoT Viet Solution
