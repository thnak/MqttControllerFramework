# Server-Side Publish (Server Actions)

## Overview

`IMqttClientActionService` lets the broker publish messages to MQTT topics from server-side code — background jobs, API endpoints, domain events, etc. Server-originated messages are automatically tagged with a user property so the broker ignores its own traffic (prevents infinite loops when the server also consumes its own topics).

---

## Interface

```csharp
public interface IMqttClientActionService
{
    // Publish raw bytes
    Task SendMessageAsync(
        string topic,
        ReadOnlySequence<byte> message,
        CancellationToken cancellationToken = default);

    // Publish bytes and await a typed response on the reply topic
    Task<TResult?> SendMessageAsync<TResult>(
        string topic,
        ReadOnlySequence<byte> message,
        CancellationToken cancellationToken = default);

    // Send an empty request and await a typed response
    Task<TResult?> GetDataFromTopicAsync<TResult>(
        string topic,
        CancellationToken cancellationToken = default);
}
```

---

## Publishing from a Background Service

```csharp
public class AlertPublisher(IMqttClientActionService mqtt) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), ct);

            var alert = new AlertMessage { Level = "warning", Text = "High temperature" };
            var bytes = JsonSerializer.SerializeToUtf8Bytes(alert);
            await mqtt.SendMessageAsync("alerts/temperature", new ReadOnlySequence<byte>(bytes), ct);
        }
    }
}
```

---

## Publishing from a Minimal API Endpoint

```csharp
app.MapPost("/api/devices/{deviceId}/command", async (
    string deviceId,
    DeviceCommand command,
    IMqttClientActionService mqtt,
    CancellationToken ct) =>
{
    var payload = JsonSerializer.SerializeToUtf8Bytes(command);
    await mqtt.SendMessageAsync(
        $"devices/{deviceId}/commands",
        new ReadOnlySequence<byte>(payload),
        ct);

    return Results.Accepted();
});
```

---

## Request-Response via MQTT

Use `SendMessageAsync<TResult>` to publish and wait for a response on a reply topic:

```csharp
var request = JsonSerializer.SerializeToUtf8Bytes(new StatusRequest { Verbose = true });
var status = await mqtt.SendMessageAsync<DeviceStatus>(
    $"devices/{deviceId}/status/request",
    new ReadOnlySequence<byte>(request),
    ct);

if (status is not null)
    Console.WriteLine($"Device status: {status.State}");
```

Or use `GetDataFromTopicAsync<T>` when no payload is needed in the request:

```csharp
var latest = await mqtt.GetDataFromTopicAsync<SensorReading>("sensors/room1/temperature/latest", ct);
```

---

## Self-Loop Prevention

All messages published via `IMqttClientActionService` include an MQTT user property:

```
x-mqtt-origin: server
```

The broker intercepts this property and marks the message with `ProcessPublish = false`, so the framework will not re-dispatch server-originated messages to controllers. The header name and value are configurable via `MqttSettings`:

```json
"MqttSettings": {
  "ServerOriginPropertyName": "x-mqtt-origin",
  "ServerOriginPropertyValue": "server"
}
```

---

## Notes

- `IMqttClientActionService` is registered as a **singleton**.
- Inject it into any service that has access to the DI container.
- The request-response overloads rely on the client setting a `ResponseTopic` on outbound publishes — the framework's generated dispatchers handle this on the server side when a controller method returns a value.
