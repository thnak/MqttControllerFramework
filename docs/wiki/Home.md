# MqttControllerFramework — Wiki

Welcome to the documentation for **MqttControllerFramework**, a source-generated MQTT controller framework for .NET built on [MQTTnet 5](https://github.com/dotnet/MQTTnet).

## Pages

| Topic | Description |
|---|---|
| [Controllers and Routing](Controllers-and-Routing.md) | Defining controllers, topic templates, parameter binding, custom parsers, compile-time diagnostics |
| [Middleware](Middleware.md) | Pipeline, `MqttMessageContext`, `SessionItems`, per-message state |
| [Authentication](Authentication.md) | Username/password auth, lockout support |
| [Authorization](Authorization.md) | Publish/subscribe authorization, roles, policies, `[MqttAuthorize]` |
| [Connection Validation](Connection-Validation.md) | Pre-auth hooks, IP/ClientId filtering, session-item seeding, auth bypass |
| [Rate Limiting](Rate-Limiting.md) | Token-bucket rate limiting per route |
| [TLS and Security](TLS-and-Security.md) | PKCS#12, PEM, hot-swap, client certificate allowlist |
| [Retained Messages](Retained-Messages.md) | Pluggable retain storage — file, database, Redis, or custom |
| [Events](Events.md) | Client connected / disconnected / subscribed / unsubscribed |
| [Server Actions](Server-Actions.md) | Publishing messages from the broker to topics |
| [Broker Statistics](Broker-Stats.md) | Message counters, byte throughput, per-topic summaries |
| [Configuration Reference](Configuration-Reference.md) | All `MqttSettings` options |

## At a Glance

```
[MqttController]        ← marks the class as a controller
[MqttTopic("a/+/b")]    ← maps a method to a topic template
[FromMqttTopic(1)]      ← binds a parameter to the 1st '+' wildcard segment
[MqttAuthorize]         ← requires authorization for this handler
[MqttAllowAnonymous]    ← bypasses class-level [MqttAuthorize]
[TokenBucketRateLimit]  ← applies token-bucket rate limiting
```

The **source generator** reads these attributes at build time and emits:

- `GeneratedMqttControllerRegistration` — `IMqttControllerRegistration` passed to `WithControllers<>()`
- `{Controller}Dispatcher` — one per controller, handles deserialization and method dispatch
- `GeneratedMqttRoutingService` — DFA-based topic router with an LRU cache; no reflection at runtime

MQTT001 and MQTT002 diagnostics flag duplicate and ambiguous topic patterns at **build time**, before any code runs.
