# Broker Statistics

## Overview

`IMqttBrokerStatsService` exposes real-time metrics for the running broker. Inject it into any service, controller, or API endpoint to expose stats over HTTP, push them to a metrics system, or drive health checks.

---

## Injecting the Service

```csharp
public class MetricsEndpoints(IMqttBrokerStatsService stats) : IHostedService
{
    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

Or in a minimal API:

```csharp
app.MapGet("/metrics/mqtt", (IMqttBrokerStatsService stats) => new
{
    messages   = stats.GetTotalMessageCount(),
    clients    = stats.GetTotalConnectedClientCount(),
    uptime     = stats.GetUptime().ToString(),
    bytesIn    = stats.GetTotalBytesReceivedByBroker(),
    bytesOut   = stats.GetTotalBytesSentByBroker(),
    topics     = stats.GetTopicSummary(),
});
```

---

## Available Methods

### Counters

| Method | Returns | Description |
|---|---|---|
| `GetTotalMessageCount()` | `ulong` | Total messages received |
| `GetTotalMessageSize()` | `ulong` | Total payload bytes received |
| `GetDroppedMessageCount()` | `ulong` | Messages dropped (rate limited or rejected) |
| `GetUnconsumedMessageCount()` | `ulong` | Messages published to a topic with no matching route |
| `GetAcknowledgedMessageCount()` | `ulong` | QoS 1/2 messages acknowledged |
| `GetQueueOverwriteCount()` | `ulong` | Persistent queue overwrites |

### Throughput

| Method | Returns | Description |
|---|---|---|
| `GetTotalBytesReceivedByBroker()` | `ulong` | Bytes received by the broker |
| `GetTotalBytesSentByBroker()` | `ulong` | Bytes sent by the broker |
| `GetTotalBytesReceivedByClients()` | `ulong` | Total bytes received across all clients |
| `GetTotalBytesSentByClients()` | `ulong` | Total bytes sent to all clients |

### Connection and session

| Method | Returns | Description |
|---|---|---|
| `GetTotalConnectedClientCount()` | `ulong` | Currently connected clients |
| `GetSessionCount()` | `int` | Active sessions (including persistent) |
| `GetSubscriptionCount()` | `int` | Total active subscriptions |
| `GetRetainedMessageCount()` | `ulong` | Stored retained messages |

### Per-topic summary

```csharp
Dictionary<string, TopicStatistics> summary = stats.GetTopicSummary();
foreach (var (topic, ts) in summary)
    Console.WriteLine($"{topic}: {ts.MessageCount} msgs, {ts.ByteCount} bytes");
```

`TopicStatistics` contains message count and byte count per topic.

### Uptime and availability

| Method | Returns | Description |
|---|---|---|
| `GetUptime()` | `TimeSpan` | Time since the broker started |
| `GetAcceptNewConnections()` | `bool` | Whether the broker is accepting new connections |
| `SetAcceptNewConnections(bool)` | `void` | Enable or disable new connections (graceful drain) |

---

## Graceful Drain Example

Stop accepting new connections before a maintenance window while letting existing clients finish:

```csharp
app.MapPost("/admin/drain", (IMqttBrokerStatsService stats) =>
{
    stats.SetAcceptNewConnections(false);
    return Results.Ok("Broker is draining — no new connections accepted.");
});

app.MapPost("/admin/resume", (IMqttBrokerStatsService stats) =>
{
    stats.SetAcceptNewConnections(true);
    return Results.Ok("Broker resumed accepting connections.");
});
```

---

## Notes

- `IMqttBrokerStatsService` is registered as a **singleton** and is thread-safe.
- Counters are `ulong` and do not overflow on 64-bit platforms for realistic workloads.
- `MarkBrokerStarted()` is called internally by the hosted service — do not call it manually.
