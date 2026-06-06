# Retained Messages

## Overview

A retained MQTT message is stored by the broker and delivered to any client that subscribes to the matching topic — even if the publisher has long since disconnected. MqttControllerFramework persists retained messages across restarts via the `IRetainStorage` interface.

The built-in default is **file-based JSON** (`FileRetainStorage`). Replace it with a custom implementation to store retained messages in a database, Redis, blob storage, or any other backend.

---

## Default Behaviour

Out of the box, retained messages are written to a JSON file whenever they change. The path is controlled by `MqttSettings.RetainedMessagesFilePath`:

```json
"MqttSettings": {
  "RetainedMessagesFilePath": "RetainedMessages.json"
}
```

The file is created automatically. If it does not exist at startup the broker starts with an empty retained message set.

---

## `IRetainStorage`

```csharp
public interface IRetainStorage
{
    // Called on broker startup — return all previously persisted messages
    Task<IReadOnlyList<MqttApplicationMessage>> LoadAsync(CancellationToken cancellationToken = default);

    // Called after any change — messages is the complete current snapshot
    Task SaveAsync(IReadOnlyList<MqttApplicationMessage> messages, CancellationToken cancellationToken = default);

    // Called when the broker clears all retained messages
    Task ClearAsync(CancellationToken cancellationToken = default);
}
```

`SaveAsync` receives the **full snapshot** of retained messages every time any retained message changes. `LoadAsync` is called once at startup.

---

## Custom Implementation

### Example: in-memory (no persistence)

```csharp
public class InMemoryRetainStorage : IRetainStorage
{
    private IReadOnlyList<MqttApplicationMessage> _messages = [];

    public Task<IReadOnlyList<MqttApplicationMessage>> LoadAsync(CancellationToken ct = default)
        => Task.FromResult(_messages);

    public Task SaveAsync(IReadOnlyList<MqttApplicationMessage> messages, CancellationToken ct = default)
    {
        _messages = messages;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        _messages = [];
        return Task.CompletedTask;
    }
}
```

### Example: SQL Server

```csharp
public class SqlRetainStorage(IDbConnectionFactory db) : IRetainStorage
{
    public async Task<IReadOnlyList<MqttApplicationMessage>> LoadAsync(CancellationToken ct = default)
    {
        await using var conn = await db.OpenAsync(ct);
        var rows = await conn.QueryAsync<RetainedRow>("SELECT Topic, Payload, ContentType FROM RetainedMessages");
        return rows.Select(r => new MqttApplicationMessageBuilder()
            .WithTopic(r.Topic)
            .WithPayload(r.Payload)
            .WithContentType(r.ContentType)
            .WithRetainFlag(true)
            .Build()).ToList();
    }

    public async Task SaveAsync(IReadOnlyList<MqttApplicationMessage> messages, CancellationToken ct = default)
    {
        await using var conn = await db.OpenAsync(ct);
        await conn.ExecuteAsync("DELETE FROM RetainedMessages");
        foreach (var m in messages)
            await conn.ExecuteAsync(
                "INSERT INTO RetainedMessages(Topic, Payload, ContentType) VALUES(@Topic, @Payload, @ContentType)",
                new { m.Topic, Payload = m.Payload.ToArray(), m.ContentType });
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        await using var conn = await db.OpenAsync(ct);
        await conn.ExecuteAsync("DELETE FROM RetainedMessages");
    }
}
```

### Registration

```csharp
builder.Services
    .AddMqttServer(configuration)
    .WithControllers<GeneratedMqttControllerRegistration>()
    .WithAuthentication<MyAuthProvider>()
    .WithRetainStorage<SqlRetainStorage>();  // replaces FileRetainStorage
```

`WithRetainStorage<T>()` registers your implementation as a singleton and replaces the built-in file backend.

---

## Notes

- `IRetainStorage` is registered as a **singleton**; it must be thread-safe.
- `SaveAsync` is called on the broker's event thread — keep implementations fast. Offload heavy I/O to a background queue if needed.
- Setting `RetainedMessagesFilePath` has no effect when a custom `IRetainStorage` is registered.
