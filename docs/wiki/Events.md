# Lifecycle Events

## Overview

Four event interfaces let you react to client lifecycle changes. Multiple handlers can be registered for each event; they are all invoked sequentially.

| Interface | Builder method | Triggered when |
|---|---|---|
| `IMqttClientConnectedEvent` | `.OnClientConnected<T>()` | A client successfully connects |
| `IMqttClientDisconnectedEvent` | `.OnClientDisconnected<T>()` | A client disconnects |
| `IMqttClientSubscribedTopicEvent` | `.OnClientSubscribedTopic<T>()` | A client subscribes to a topic filter |
| `IMqttClientUnsubscribedTopicEvent` | `.OnClientUnsubscribedTopic<T>()` | A client unsubscribes from a topic filter |

---

## `IMqttClientConnectedEvent`

```csharp
public interface IMqttClientConnectedEvent
{
    Task OnClientConnectedAsync(string clientId);
}
```

Example — notify a presence service:

```csharp
public class PresenceConnectedHandler(IPresenceService presence) : IMqttClientConnectedEvent
{
    public Task OnClientConnectedAsync(string clientId)
        => presence.SetOnlineAsync(clientId);
}
```

---

## `IMqttClientDisconnectedEvent`

```csharp
public interface IMqttClientDisconnectedEvent
{
    Task OnClientDisconnectedAsync(string clientId);
}
```

Example — clean up resources:

```csharp
public class PresenceDisconnectedHandler(IPresenceService presence) : IMqttClientDisconnectedEvent
{
    public Task OnClientDisconnectedAsync(string clientId)
        => presence.SetOfflineAsync(clientId);
}
```

---

## `IMqttClientSubscribedTopicEvent`

```csharp
public interface IMqttClientSubscribedTopicEvent
{
    Task OnClientSubscribedTopicAsync(string clientId, string topicFilter);
}
```

Example — audit log:

```csharp
public class SubscribeAuditHandler(IAuditLog audit) : IMqttClientSubscribedTopicEvent
{
    public Task OnClientSubscribedTopicAsync(string clientId, string topicFilter)
        => audit.RecordAsync($"SUBSCRIBE {clientId} → {topicFilter}");
}
```

---

## `IMqttClientUnsubscribedTopicEvent`

```csharp
public interface IMqttClientUnsubscribedTopicEvent
{
    Task OnClientUnsubscribedTopicAsync(string clientId, string topicFilter);
}
```

---

## Registration

Chain all the handlers you need:

```csharp
builder.Services
    .AddMqttServer(configuration)
    .WithControllers<GeneratedMqttControllerRegistration>()
    .WithAuthentication<MyAuthProvider>()
    .OnClientConnected<PresenceConnectedHandler>()
    .OnClientConnected<AuditConnectedHandler>()         // two handlers for the same event
    .OnClientDisconnected<PresenceDisconnectedHandler>()
    .OnClientSubscribedTopic<SubscribeAuditHandler>()
    .OnClientUnsubscribedTopic<UnsubscribeAuditHandler>();
```

All handlers are registered as **scoped** services and resolved from a new scope per event.
