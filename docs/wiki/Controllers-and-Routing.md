# Controllers and Routing

## Overview

Controllers are plain C# classes decorated with `[MqttController]`. Each public method decorated with `[MqttTopic]` becomes a topic handler. The source generator scans these at build time and emits a DFA-based routing service — there is no reflection or dictionary lookup at runtime.

---

## `[MqttController]`

```csharp
[MqttController]                      // no prefix
public class SensorsController { }

[MqttController("devices")]           // all routes prefixed with "devices/"
public class DeviceController { }
```

`prefix` is prepended to every `[MqttTopic]` template in the class, separated by `/`.

---

## `[MqttTopic]`

```csharp
[MqttTopic("sensors/+/temperature")]  // single-level wildcard
[MqttTopic("logs/#")]                 // multi-level wildcard (must be last segment)
[MqttTopic("system/status")]          // exact match
```

MQTT wildcard rules apply:
- `+` matches exactly one topic level.
- `#` matches the rest of the topic; must be the last segment.

The **final template** is `{prefix}/{template}` with empty parts stripped. A controller with `prefix = "devices"` and template `"+/telemetry"` produces `"devices/+/telemetry"`.

---

## Parameter Binding

The generator resolves method parameters in this order:

| Parameter type | Resolution |
|---|---|
| Decorated with `[FromMqttTopic(n)]` | Extracted from the *n*-th `+` wildcard segment of the topic |
| `CancellationToken` | Forwarded from MQTTnet |
| `byte[]` | Raw payload bytes |
| `InterceptingPublishEventArgs` | Full MQTTnet event args |
| Implements `IMqttPayloadParser<T>` | Custom binary parser (see below) |
| Any other type | JSON-deserialized from the payload |

### Topic segment binding — `[FromMqttTopic]`

```csharp
[MqttTopic("devices/+/sensors/+/data")]
public Task OnData(
    [FromMqttTopic(1)] string deviceId,   // first '+'
    [FromMqttTopic(2)] string sensorId,   // second '+'
    SensorReading payload)
{ }
```

For `enum` parameters the segment is parsed with `Enum.TryParse` (case-insensitive). For all other types `Convert.ChangeType` is used.

> **Tip:** Named parameters work the same way — the name in the method signature does not need to match the topic segment; only the `[FromMqttTopic(index)]` matters.

### Named-segment shorthand

If your topic template uses `{name}` style segments instead of bare `+`, declare the parameter with the matching name and no attribute:

```csharp
[MqttController("sensors")]
public class SensorsController
{
    [MqttTopic("{deviceId}/temperature")]
    public async Task OnTemperature(string deviceId, TemperaturePayload payload) { }
}
```

The generator maps the parameter by name to the corresponding `+` position in the compiled template (`sensors/+/temperature`).

---

## Custom Payload Parsers

For non-JSON payloads implement `IMqttPayloadParser<T>` on the payload type and tag it with `[MqttPayloadContentType("...")]`:

```csharp
[MqttPayloadContentType("application/x-messagepack")]
public class MessagePackSensorReading : IMqttPayloadParser<MessagePackSensorReading>
{
    public double Value { get; set; }
    public string Unit { get; set; } = "";

    public MessagePackSensorReading Parse(ReadOnlySpan<byte> payload)
        => MessagePackSerializer.Deserialize<MessagePackSensorReading>(payload.ToArray());
}
```

Use it as a parameter type:

```csharp
[MqttTopic("sensors/+/packed")]
public Task OnPacked([FromMqttTopic(1)] string id, MessagePackSensorReading data) { }
```

The generator registers the parser automatically for the declared content-type. If the incoming message has a matching `ContentType` property the custom parser is used; otherwise the framework falls back to JSON.

---

## Request-Response Pattern

A handler can return `Task<TResponse>`. If the incoming message has a `ResponseTopic`, the framework serializes the return value as JSON and publishes it back:

```csharp
[MqttTopic("queries/+/status")]
public async Task<DeviceStatus> GetStatus([FromMqttTopic(1)] string deviceId)
{
    return await _repository.GetStatusAsync(deviceId);
}
```

The client sets `ResponseTopic` on its publish; the broker sends the JSON response to that topic automatically.

---

## Controller DI

Controllers are registered as **scoped** services. Inject any scoped or singleton dependency via the constructor:

```csharp
[MqttController("telemetry")]
public class TelemetryController(IDataRepository repo, ILogger<TelemetryController> log)
{
    [MqttTopic("+/readings")]
    public async Task OnReading([FromMqttTopic(1)] string deviceId, SensorData data)
    {
        await repo.SaveAsync(deviceId, data);
        log.LogInformation("Saved reading from {DeviceId}", deviceId);
    }
}
```

---

## Generated Code

At build time the generator emits three files into `<YourAssembly>.Mqtt.Generated`:

| File | Contents |
|---|---|
| `MqttControllerRegistration.g.cs` | `GeneratedMqttControllerRegistration : IMqttControllerRegistration` |
| `{Controller}Dispatcher.g.cs` | Per-controller dispatcher and `I{Controller}Dispatcher` interface |
| `GeneratedMqttRoutingService.g.cs` | DFA topic matcher + `RouteMatchCache` (128-entry LRU) |

Reference `GeneratedMqttControllerRegistration` in `WithControllers<>()`. Add a using for the generated namespace if needed:

```csharp
using MyApp.Mqtt.Generated;

builder.Services
    .AddMqttServer(configuration)
    .WithControllers<GeneratedMqttControllerRegistration>();
```
