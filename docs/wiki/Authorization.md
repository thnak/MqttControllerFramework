# Authorization

## Overview

Authorization controls which clients can **publish** to a topic or **subscribe** to a topic filter. It is optional — if no `IMqttAuthorizationProvider` is registered, the broker skips publish/subscribe authorization entirely.

---

## `IMqttAuthorizationProvider`

```csharp
public interface IMqttAuthorizationProvider
{
    ValueTask<MqttAuthorizationResult> AuthorizePublishAsync(
        string userName, string topic, int qos, bool retain,
        CancellationToken cancellationToken = default);

    ValueTask<MqttAuthorizationResult> AuthorizeSubscribeAsync(
        string userName, string topicFilter, int requestedQos,
        CancellationToken cancellationToken = default);
}
```

Return `MqttAuthorizationResult.Allow()` to permit or `MqttAuthorizationResult.Deny(reason)` to block.

---

## Minimal Example

```csharp
public class RoleBasedAuthzProvider(IUserRoleStore roles) : IMqttAuthorizationProvider
{
    public async ValueTask<MqttAuthorizationResult> AuthorizePublishAsync(
        string userName, string topic, int qos, bool retain, CancellationToken ct)
    {
        var userRoles = await roles.GetAsync(userName, ct);

        // Only "device" role may publish to "sensors/#"
        if (topic.StartsWith("sensors/") && !userRoles.Contains("device"))
            return MqttAuthorizationResult.Deny("Insufficient role to publish sensor data");

        return MqttAuthorizationResult.Allow();
    }

    public ValueTask<MqttAuthorizationResult> AuthorizeSubscribeAsync(
        string userName, string topicFilter, int requestedQos, CancellationToken ct)
        => ValueTask.FromResult(MqttAuthorizationResult.Allow()); // open subscriptions
}
```

Register:

```csharp
builder.Services
    .AddMqttServer(configuration)
    .WithControllers<GeneratedMqttControllerRegistration>()
    .WithAuthentication<MyAuthProvider>()
    .WithAuthorization<RoleBasedAuthzProvider>();
```

---

## `[MqttAuthorize]` Attribute

Apply to a controller class or a specific method. When the route is dispatched, the framework calls `AuthorizePublishAsync` before invoking the handler.

```csharp
[MqttController]
[MqttAuthorize]                    // all handlers in this controller require authorization
public class AdminController
{
    [MqttTopic("admin/+/command")]
    public Task OnCommand([FromMqttTopic(1)] string deviceId, AdminCommand cmd) { ... }

    [MqttTopic("admin/ping")]
    [MqttAllowAnonymous]           // override class-level — no auth required
    public Task OnPing() { ... }
}
```

Attribute properties:

| Property | Type | Description |
|---|---|---|
| `Roles` | `string?` | Comma-separated list of allowed roles |
| `Policies` | `string?` | Comma-separated list of required policies |
| `Policy` | `string?` | Named authorization policy |
| `RequireAllRoles` | `bool` | `true` = AND logic; `false` (default) = OR logic |
| `UnauthorizedMessage` | `string?` | Custom message sent to client on denial |

---

## `[MqttAllowAnonymous]`

Bypasses `[MqttAuthorize]` applied at the class level for a specific method:

```csharp
[MqttController]
[MqttAuthorize]
public class SecureController
{
    [MqttTopic("secure/data")]
    public Task OnData(DataPayload payload) { ... }   // requires authorization

    [MqttTopic("secure/health")]
    [MqttAllowAnonymous]
    public Task OnHealth() { ... }                    // bypasses authorization
}
```

---

## `MqttAuthorizationResult` Properties

| Property | Type | Description |
|---|---|---|
| `IsAuthorized` | `bool` | `true` when the request is authorized |
| `DenialReason` | `string?` | Human-readable reason for denial (logged and returned to client) |
| `MaxQoS` | `int?` | Cap the QoS level the client may use — `null` means no constraint |
| `AllowRetain` | `bool?` | Whether the retain flag is permitted — `null` means no constraint |

### Constraining QoS

```csharp
// Allow publish but cap at QoS 1
return new MqttAuthorizationResult { IsAuthorized = true, MaxQoS = 1 };

// Or use Deny with a cap (note: IsAuthorized = false here)
return MqttAuthorizationResult.Deny("QoS 2 not allowed", maxQoS: 1);
```

---

## Notes

- Authorization runs **after** authentication.
- The provider is registered as **scoped**, so it can inject `DbContext` or other scoped services.
- Route-level `[MqttAuthorize]` authorization is enforced by the generated routing service; the provider's `AuthorizePublishAsync` is called inline for each matching route.
