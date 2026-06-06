# Rate Limiting

## Overview

MqttControllerFramework includes a **token-bucket rate limiter** that enforces per-client, per-route message limits. Apply `[TokenBucketRateLimit]` to any topic handler and call `.WithRateLimiting()` on the builder.

---

## Enabling

```csharp
builder.Services
    .AddMqttServer(configuration)
    .WithControllers<GeneratedMqttControllerRegistration>()
    .WithAuthentication<MyAuthProvider>()
    .WithRateLimiting();   // ← registers the rate limit service
```

---

## `[TokenBucketRateLimit]`

```csharp
[TokenBucketRateLimit(capacity, refillRate, refillIntervalMs)]
```

| Parameter | Type | Description |
|---|---|---|
| `capacity` | `int` | Maximum tokens in the bucket (burst limit) |
| `refillRate` | `int` | Tokens added per refill interval |
| `refillIntervalMs` | `int` | Refill interval in milliseconds |
| `TokensPerRequest` | `int` | Tokens consumed per message (default: 1) |

### Example: allow up to 10 messages/second with a burst of 10

```csharp
[MqttController]
public class TelemetryController
{
    [MqttTopic("telemetry/+/data")]
    [TokenBucketRateLimit(capacity: 10, refillRate: 10, refillIntervalMs: 1000)]
    public Task OnData([FromMqttTopic(1)] string deviceId, SensorData data) { ... }
}
```

Token bucket behaviour:
- Bucket starts full (`capacity` tokens).
- Each incoming message consumes `TokensPerRequest` tokens.
- Tokens refill at `refillRate` per `refillIntervalMs`.
- If the bucket is empty the message is **dropped** (not queued).

### Example: high-cost operation — 5 tokens per request

```csharp
[MqttTopic("bulk/+/import")]
[TokenBucketRateLimit(capacity: 50, refillRate: 10, refillIntervalMs: 1000, TokensPerRequest = 5)]
public Task OnBulkImport([FromMqttTopic(1)] string deviceId, BulkPayload payload) { ... }
```

### Stacking multiple limits

Multiple attributes can be applied to the same method (e.g. a burst limit and a sustained limit):

```csharp
[MqttTopic("commands/+/execute")]
[TokenBucketRateLimit(capacity: 5,  refillRate: 5,  refillIntervalMs: 1000,  Name = "burst")]
[TokenBucketRateLimit(capacity: 60, refillRate: 60, refillIntervalMs: 60000, Name = "sustained")]
public Task OnExecute([FromMqttTopic(1)] string deviceId, Command cmd) { ... }
```

---

## How Limits Are Keyed

By default the rate limit applies per **ClientId + route**. Each connecting client gets its own token bucket for each rate-limited route.

---

## Custom Rate Limit Strategy

Implement `IRateLimitStrategy` and create a custom attribute that inherits `RateLimitAttribute` to add strategies beyond the built-in token bucket:

```csharp
public sealed class SlidingWindowRateLimitAttribute(int windowMs, int maxRequests)
    : RateLimitAttribute
{
    public override string StrategyType => "SlidingWindow";
    public override bool ValidateConfiguration() => windowMs > 0 && maxRequests > 0;
    public override Dictionary<string, object> GetConfiguration() => new()
    {
        ["WindowMs"] = windowMs,
        ["MaxRequests"] = maxRequests
    };
}
```

Then implement `IRateLimitStrategy` and register it with the DI container.

---

## Notes

- Rate limiting is enforced by the generated routing service before the controller dispatcher runs.
- Messages dropped by the rate limiter are counted in `IMqttBrokerStatsService.GetDroppedMessageCount()`.
- `TokensPerRequest` must be `> 0` and `<= capacity` — an invalid configuration is detected at build time via `ValidateConfiguration()`.
