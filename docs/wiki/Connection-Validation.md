# Connection Validation

## Overview

`IMqttConnectionValidator` is an optional pre-authentication hook called for every incoming CONNECT packet **before** the authentication provider runs. Use it to:

- Enforce ClientId format rules
- Block specific IP addresses or subnets
- Pre-populate `SessionItems` with connection-level data (tenant ID, roles, etc.) so subsequent middleware and controllers do not need to repeat the lookup per message

---

## Interface

```csharp
public interface IMqttConnectionValidator
{
    ValueTask<MqttConnectionValidationResult> ValidateAsync(
        ValidatingConnectionEventArgs context,
        CancellationToken cancellationToken = default);
}
```

`ValidatingConnectionEventArgs` is the raw MQTTnet event args — it exposes `ClientId`, `UserName`, `Password`, `RemoteEndPoint`, `SessionItems`, and more.

---

## Result

| Factory | Meaning |
|---|---|
| `MqttConnectionValidationResult.Accept()` | Allow the connection to proceed |
| `MqttConnectionValidationResult.Reject(reason, code?)` | Refuse immediately |

`reason` is logged server-side; `code` is the `MqttConnectReasonCode` sent to the client (default: `NotAuthorized`).

---

## Example: ClientId format check

```csharp
public class ClientIdValidator : IMqttConnectionValidator
{
    // Expect ClientIds like "device-{guid}"
    private static readonly Regex _pattern =
        new(@"^device-[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public ValueTask<MqttConnectionValidationResult> ValidateAsync(
        ValidatingConnectionEventArgs ctx, CancellationToken ct)
    {
        if (!_pattern.IsMatch(ctx.ClientId))
            return ValueTask.FromResult(
                MqttConnectionValidationResult.Reject(
                    $"Invalid ClientId format: '{ctx.ClientId}'"));

        return ValueTask.FromResult(MqttConnectionValidationResult.Accept());
    }
}
```

---

## Example: IP allowlist

```csharp
public class IpAllowlistValidator(IIpAllowlist allowlist) : IMqttConnectionValidator
{
    public async ValueTask<MqttConnectionValidationResult> ValidateAsync(
        ValidatingConnectionEventArgs ctx, CancellationToken ct)
    {
        var ip = (ctx.RemoteEndPoint as System.Net.IPEndPoint)?.Address;
        if (ip is not null && !await allowlist.IsAllowedAsync(ip, ct))
            return MqttConnectionValidationResult.Reject(
                $"IP {ip} is not on the allowlist",
                MqttConnectReasonCode.Banned);

        return MqttConnectionValidationResult.Accept();
    }
}
```

---

## Example: Seeding SessionItems

Populate `SessionItems` once here; middleware reads the values on every subsequent message without extra lookups:

```csharp
public class TenantConnectionValidator(ITenantStore tenants) : IMqttConnectionValidator
{
    public async ValueTask<MqttConnectionValidationResult> ValidateAsync(
        ValidatingConnectionEventArgs ctx, CancellationToken ct)
    {
        var tenant = await tenants.FindByUsernameAsync(ctx.UserName, ct);
        if (tenant is null)
            return MqttConnectionValidationResult.Reject("Tenant not found");

        // Stored for the lifetime of the TCP connection
        ctx.SessionItems["tenantId"]  = tenant.Id;
        ctx.SessionItems["tenantPlan"] = tenant.Plan;

        return MqttConnectionValidationResult.Accept();
    }
}
```

In middleware:

```csharp
public class TenantMiddleware(ITenantContext ctx) : IMqttMiddleware
{
    public Task InvokeAsync(MqttMessageContext context, MqttRequestDelegate next)
    {
        if (context.SessionItems["tenantId"] is string tid)
            ctx.SetTenant(tid);
        return next(context);
    }
}
```

---

## Registration

```csharp
builder.Services
    .AddMqttServer(configuration)
    .WithConnectionValidator<TenantConnectionValidator>()
    .WithAuthentication<MyAuthProvider>()
    .WithControllers<GeneratedMqttControllerRegistration>();
```

---

## Notes

- The validator runs **before** authentication. You can reject the connection before any credentials are checked.
- The validator is registered as **scoped**.
- `SessionItems` is an `IDictionary` (non-generic) backed by MQTTnet. Cast values to the expected type when reading.
