# Configuration Reference

## `MqttSettings`

Bound from the `"MqttSettings"` section of `appsettings.json` (or any `IConfiguration` source). Pass the configuration root to `AddMqttServer(configuration)`.

```json
"MqttSettings": {
  "EnableNonSsl": true,
  "NonSslPort": 1883,
  "EnableSsl": false,
  "SslPort": 8883,
  "PkcsPath": "",
  "PkcsPassword": "",
  "PkcsKeyPath": "",
  "ClientCertStorage": "",
  "RetainedMessagesFilePath": "RetainedMessages.json",
  "ServerOriginPropertyName": "x-mqtt-origin",
  "ServerOriginPropertyValue": "server"
}
```

---

## Property Reference

### Network

| Property | Type | Default | Description |
|---|---|---|---|
| `EnableNonSsl` | `bool` | `true` | Listen on a plain TCP port |
| `NonSslPort` | `int` | `1883` | Plain TCP port number |
| `EnableSsl` | `bool` | `false` | Listen on a TLS port |
| `SslPort` | `int` | `8883` | TLS port number |

### TLS Certificates

| Property | Type | Default | Description |
|---|---|---|---|
| `PkcsPath` | `string` | `""` | Path to the PKCS#12 (`.pfx`) or PEM certificate (`.crt`) file |
| `PkcsPassword` | `string` | `""` | Password for the PKCS#12 bundle; leave empty for PEM |
| `PkcsKeyPath` | `string` | `""` | Path to the PEM private-key file (`.key`). When set, `PkcsPath` is treated as the public certificate |
| `ClientCertStorage` | `string` | `""` | Directory for persisting trusted client certificates (`.cer`). Loaded on startup if set |

### Retained Messages

| Property | Type | Default | Description |
|---|---|---|---|
| `RetainedMessagesFilePath` | `string` | `"RetainedMessages.json"` | File path for retained message persistence across restarts |

### Self-Loop Prevention

| Property | Type | Default | Description |
|---|---|---|---|
| `ServerOriginPropertyName` | `string` | `"x-mqtt-origin"` | MQTT user-property name added to server-originated messages |
| `ServerOriginPropertyValue` | `string` | `"server"` | Value of that property used to identify server traffic |

---

## Minimal Configurations

### Plain TCP only (development)

```json
"MqttSettings": {
  "EnableNonSsl": true,
  "NonSslPort": 1883
}
```

### TLS only (production)

```json
"MqttSettings": {
  "EnableNonSsl": false,
  "EnableSsl": true,
  "SslPort": 8883,
  "PkcsPath": "/etc/certs/broker.pfx",
  "PkcsPassword": "changeme"
}
```

### Both ports (migration / dev-prod parity)

```json
"MqttSettings": {
  "EnableNonSsl": true,
  "NonSslPort": 1883,
  "EnableSsl": true,
  "SslPort": 8883,
  "PkcsPath": "/etc/certs/broker.pfx",
  "PkcsPassword": "changeme"
}
```

### PEM certificate + private key

```json
"MqttSettings": {
  "EnableSsl": true,
  "SslPort": 8883,
  "PkcsPath": "/etc/certs/broker.crt",
  "PkcsKeyPath": "/etc/certs/broker.key"
}
```

---

## Environment Variables

`MqttSettings` values can also be set via environment variables using the `__` separator (standard .NET configuration convention):

```bash
MqttSettings__EnableSsl=true
MqttSettings__SslPort=8883
MqttSettings__PkcsPath=/etc/certs/broker.pfx
MqttSettings__PkcsPassword=changeme
```

This is the recommended approach for Docker and Kubernetes deployments.

---

## Docker Compose Example

```yaml
services:
  mqtt-broker:
    image: myapp:latest
    environment:
      - MqttSettings__EnableNonSsl=false
      - MqttSettings__EnableSsl=true
      - MqttSettings__SslPort=8883
      - MqttSettings__PkcsPath=/certs/broker.pfx
      - MqttSettings__PkcsPassword=changeme
    ports:
      - "8883:8883"
    volumes:
      - ./certs:/certs:ro
```
