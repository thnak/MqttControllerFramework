# Authentication

## Overview

Authentication is **required** — the broker rejects all connections if no `IMqttAuthenticationProvider` is registered. Implement the interface and register it with `.WithAuthentication<T>()`.

---

## `IMqttAuthenticationProvider`

```csharp
public interface IMqttAuthenticationProvider
{
    ValueTask<MqttAuthenticationResult> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);
}
```

The method is called for every incoming CONNECT packet. Return one of:

| Factory | Meaning |
|---|---|
| `MqttAuthenticationResult.Authenticated()` | Connection accepted |
| `MqttAuthenticationResult.Failed(reason)` | Connection rejected — wrong credentials |
| `MqttAuthenticationResult.Locked(lockTimeRemaining)` | Connection rejected — account temporarily locked |

---

## Minimal Example

```csharp
public class ApiKeyAuthProvider : IMqttAuthenticationProvider
{
    private static readonly Dictionary<string, string> _keys = new()
    {
        ["device-001"] = "key-abc123",
        ["device-002"] = "key-def456",
    };

    public ValueTask<MqttAuthenticationResult> AuthenticateAsync(
        string username, string password, CancellationToken ct)
    {
        var result = _keys.TryGetValue(username, out var key) && key == password
            ? MqttAuthenticationResult.Authenticated()
            : MqttAuthenticationResult.Failed("Unknown device or invalid key");

        return ValueTask.FromResult(result);
    }
}
```

Register:

```csharp
builder.Services
    .AddMqttServer(configuration)
    .WithControllers<GeneratedMqttControllerRegistration>()
    .WithAuthentication<ApiKeyAuthProvider>();
```

---

## Database-Backed Example

```csharp
public class DbAuthProvider(IUserRepository users) : IMqttAuthenticationProvider
{
    public async ValueTask<MqttAuthenticationResult> AuthenticateAsync(
        string username, string password, CancellationToken ct)
    {
        var user = await users.FindAsync(username, ct);
        if (user is null)
            return MqttAuthenticationResult.Failed("User not found");

        if (user.IsLocked)
            return MqttAuthenticationResult.Locked(user.LockTimeRemaining);

        return PasswordHasher.Verify(password, user.PasswordHash)
            ? MqttAuthenticationResult.Authenticated()
            : MqttAuthenticationResult.Failed("Invalid password");
    }
}
```

---

## `MqttAuthenticationResult` Properties

| Property | Type | Description |
|---|---|---|
| `IsAuthenticated` | `bool` | `true` when authentication succeeded |
| `FailureReason` | `string?` | Human-readable failure message (logged, not sent to client) |
| `IsLocked` | `bool` | `true` when the account is temporarily locked |
| `LockTimeRemaining` | `TimeSpan?` | Duration until the lockout expires |

---

## Notes

- The provider is registered as **scoped**, so it can inject scoped services such as `DbContext`.
- `password` arrives as plain text. Hash comparison is the implementor's responsibility.
- The framework does not impose a lockout policy — implement it in the provider if needed.
