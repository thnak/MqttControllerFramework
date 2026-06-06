# Performance

## Overview

MqttControllerFramework is designed to add as little overhead as possible on top of [MQTTnet](https://github.com/dotnet/MQTTnet). Raw broker throughput is determined by MQTTnet; this page documents what the framework itself does on each publish and how to get the most out of it.

---

## Route Matching

The generated routing service uses a **two-stage strategy**:

### Stage 1 — LRU string cache (128 entries)

```
topic string  →  cache lookup  →  route id (if hit)
```

On a cache hit the route is dispatched immediately. The cache is a fixed ring-buffer of 128 topic→route-id pairs. Frequently published topics (e.g. a fixed set of device topics) will typically remain cached after the first message, making repeated dispatches a simple string scan over ≤128 entries with no pattern matching at all.

### Stage 2 — DFA pattern match (cache miss)

On a cache miss the topic is encoded to UTF-8 bytes and fed into the generated DFA:

- **Exact routes** — FNV-1a hash → `switch` → `ReadOnlySpan<byte>.SequenceEqual` against a pre-allocated static `byte[]`. Matches in O(topic-length) with SIMD assistance via `SequenceEqual`.
- **Wildcard routes** — an `unsafe`, pointer-based byte scanner marked `[MethodImpl(AggressiveInlining | AggressiveOptimization)]`. It reads the topic byte-by-byte, advancing past `/` separators for `+` segments and short-circuiting immediately for `#` patterns. No regex, no allocations.

After a successful DFA match the result is inserted into the LRU cache so the next identical topic is a cache hit.

### Implications

- **Publish the same topic repeatedly**: hits the cache from message 2 onward.
- **Avoid needlessly varying topic segments** (e.g. appending a timestamp to the topic) — it defeats the LRU cache and forces a DFA scan every time.
- **Exact routes are faster than wildcard routes**: the hash + `SequenceEqual` path is shorter than the byte scanner. If a handler receives only one well-known topic, make it exact.

---

## Zero-Reflection Dispatch

Controllers are **never invoked via reflection**. The source generator emits a dedicated `{Controller}Dispatcher` class for each controller with hand-inlined deserialization and method calls:

```csharp
// Generated — no MethodInfo.Invoke, no expression trees
public async ValueTask Dispatch_OnTemperature(
    InterceptingPublishEventArgs args, string topic, CancellationToken ct)
{
    var topicSegments = topic.Split('/');
    var deviceId = (string)Convert.ChangeType(topicSegments[1], typeof(string));

    var payload = default(TemperaturePayload);
    if (args.ApplicationMessage.Payload.Length > 0)
    {
        var ti = _typeInfoCache.TryGet(typeof(TemperaturePayload))
              ?? _jsonOptions.GetTypeInfo(typeof(TemperaturePayload));
        if (ti != null) { var r = new Utf8JsonReader(args.ApplicationMessage.Payload);
                          payload = (TemperaturePayload)JsonSerializer.Deserialize(ref r, ti)!; }
    }

    await _controller.OnTemperature(deviceId, payload!, ct);
}
```

**Topic-segment extraction** is `string.Split('/')` followed by array indexing — no parsing library, no allocations beyond the split array.  
**Enum segments** use `Enum.TryParse` (case-insensitive, no boxing for value types in .NET 8+).  
**JSON deserialization** reads directly from `ReadOnlySequence<byte>` via `Utf8JsonReader` — no intermediate `string` conversion.

---

## JSON Serialization

### Default behaviour

`JsonSerializerOptions` is resolved from DI and used with `GetTypeInfo(typeof(T))`. If a matching `JsonTypeInfo` is registered (e.g. from a source-generated context), deserialization is fully AOT-safe and allocation-minimal.

### Registering your own source-generated context

Pass a configure delegate to `WithControllers<>()`:

```csharp
[JsonSerializable(typeof(TemperaturePayload))]
[JsonSerializable(typeof(DeviceCommand))]
public partial class AppJsonContext : JsonSerializerContext { }

builder.Services
    .AddMqttServer(configuration)
    .WithControllers<GeneratedMqttControllerRegistration>(options =>
    {
        options.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
    });
```

With a source-generated context the deserializer:
- Uses no reflection
- Allocates no intermediate buffers
- Is trimmer- and AOT-safe

Without one, `JsonSerializer` falls back to reflection-based metadata — still fast for most workloads, but not AOT-compatible.

---

## Per-Message DI Scope

The framework creates **exactly one `IServiceScope` per incoming publish**:

```
rate-limit check  →  [create scope]  →  authorization  →  middleware pipeline  →  controller dispatch
                                           └─────────────────────────────────────────────────────────┘
                                                          single shared scope
```

This means `GetService<T>()` is called once per scoped service type across the entire pipeline — no double-resolution. The scope is disposed at the end of the pipeline via `await using`.

Connection-related work (validation, authentication) uses separate short-lived scopes since they run on a different event path.

**Implication**: keep scoped services lightweight to construct. If a service has an expensive constructor (e.g. opens a DB connection), consider initializing lazily or moving heavy work to the method level.

---

## Structured Logging

All framework-internal log calls are generated via `[LoggerMessage]`:

```csharp
[LoggerMessage(LogLevel.Warning, "Rate limit exceeded for client {clientId} on topic {topic}: {reason}")]
partial void LogRateLimitExceeded(string clientId, string topic, string? reason);
```

This means:
- **Zero string allocation** when the log level is disabled — the format string is never interpolated.
- **Strongly typed parameters** — no boxing of value types.
- Dispatcher-level trace logging (`LogLevel.Trace`) is compiled out in Release builds when trace is not enabled.

In production, set the minimum log level to `Information` or higher for framework namespaces to eliminate all dispatcher trace overhead:

```json
"Logging": {
  "LogLevel": {
    "MqttControllerFramework": "Warning"
  }
}
```

---

## Self-Origin Detection

Messages published by the broker via `IMqttClientActionService` carry a user property that prevents re-dispatch. The check uses a pre-encoded `ReadOnlyMemory<byte>` compared with `Span.SequenceEqual`:

```csharp
// Encoded once at startup, compared on every publish
private readonly ReadOnlyMemory<byte> _systemName = Encoding.UTF8.GetBytes(settings.ServerOriginPropertyValue);

private bool IsServerMessage(List<MqttUserProperty>? properties)
{
    var prop = properties?.FirstOrDefault(p => p.Name == _settings.ServerOriginPropertyName);
    return prop != null && prop.ValueBuffer.Span.SequenceEqual(_systemName.Span);
}
```

No string allocation on the hot path — `ValueBuffer` is compared directly as bytes.

---

## Recommendations

| Scenario | Recommendation |
|---|---|
| High-frequency single device | Use exact topic templates (`"devices/abc/telemetry"`) — hash + `SequenceEqual` is faster than the DFA scanner |
| Many distinct topics | Increase the LRU cache size by forking the framework or accepting cache miss overhead |
| Large payloads | Use `byte[]` or a custom `IMqttPayloadParser<T>` — avoids JSON overhead entirely |
| AOT / NativeAOT | Register a `JsonSerializerContext` via `WithControllers<>()` configure delegate |
| Many middleware | Keep middleware count low; each registered middleware adds a delegate allocation per message |
| Tracing overhead | Set `MqttControllerFramework` log level to `Warning` in production |
| Rate limiting | Rate-limit checks run before the DI scope is created — cheap to apply broadly |

---

## What MQTTnet Controls

The following are determined entirely by MQTTnet and are outside the framework's influence:

- TCP accept and read/write throughput
- MQTT packet serialization and parsing
- QoS 1/2 acknowledgement state machines
- WebSocket framing
- Session persistence and re-delivery
- Maximum pending messages per client (configured via `AddMqttServer` → `WithMaxPendingMessagesPerClient`)

Refer to the [MQTTnet documentation](https://github.com/dotnet/MQTTnet) for tuning those aspects.
