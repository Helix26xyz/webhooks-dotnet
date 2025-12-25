# Backend Configuration Format Reference

## Overview

The `BackendConfig` field in webhooks must be a **valid JSON object** specific to each backend type. The API will validate the JSON format and return helpful error messages if the format is incorrect.

## Validation

- **Storage**: BackendConfig is stored **encrypted** in the database using AES-256
- **API Input**: When creating/updating webhooks via API, provide BackendConfig as **plain JSON string**
- **Validation**: The API validates JSON format before accepting the webhook
- **Decryption**: Backend services automatically receive decrypted config

## Format by Backend Type

### Kafka (`BackendType: 2`)

**Required Format:**
```json
{
  "BootstrapServers": "host:port",
  "Topic": "topic-name"
}
```

**Example:**
```json
{
  "BootstrapServers": "10.10.100.93:9092",
  "Topic": "webhook-events"
}
```

**Common Mistake:** ❌ Sending just `"10.10.100.93:9092"` (missing JSON structure)

---

### RabbitMQ (`BackendType: 3`)

**Required Format:**
```json
{
  "HostName": "host",
  "QueueName": "queue-name",
  "UserName": "username",
  "Password": "password"
}
```

**Example:**
```json
{
  "HostName": "rabbitmq.example.com",
  "QueueName": "webhook-events",
  "UserName": "admin",
  "Password": "secretpass"
}
```

---

### Azure Service Bus (`BackendType: 4`)

**Required Format:**
```json
{
  "ConnectionString": "Endpoint=sb://...",
  "QueueName": "queue-name"
}
```

**Example:**
```json
{
  "ConnectionString": "Endpoint=sb://mybus.servicebus.windows.net/;SharedAccessKeyName=...",
  "QueueName": "webhooks"
}
```

---

### AWS SQS (`BackendType: 5`)

**Required Format:**
```json
{
  "QueueUrl": "https://sqs.region.amazonaws.com/...",
  "Region": "aws-region"
}
```

**Example:**
```json
{
  "QueueUrl": "https://sqs.us-east-1.amazonaws.com/123456789012/webhook-queue",
  "Region": "us-east-1"
}
```

---

### Redis (`BackendType: 6`)

**Required Format:**
```json
{
  "ConnectionString": "host:port,password=...",
  "Key": "key-name"
}
```

**Example:**
```json
{
  "ConnectionString": "localhost:6379,password=mypassword",
  "Key": "webhooks:events"
}
```

---

### Database (`BackendType: 1`)

No BackendConfig required. Events are stored in the database by default.

---

## API Endpoints

### Test Connection
```bash
POST /api/webhooks/test-connection
Content-Type: application/json

{
  "backendType": 2,
  "backendConfig": "{\"BootstrapServers\":\"10.10.100.93:9092\",\"Topic\":\"webhooks\"}"
}
```

### Create Webhook
```bash
POST /api/webhooks
Content-Type: application/json

{
  "name": "My Kafka Webhook",
  "slug": "my-kafka-webhook",
  "backendType": 2,
  "backendConfig": "{\"BootstrapServers\":\"10.10.100.93:9092\",\"Topic\":\"webhooks\"}",
  "owner": "my-org",
  "project": "my-project"
}
```

## Error Messages

If you provide an invalid format, you'll receive an error like:

```json
{
  "success": false,
  "message": "Invalid BackendConfig format. Kafka requires JSON format: {\"BootstrapServers\":\"host:port\",\"Topic\":\"topic-name\"}"
}
```

## Security Notes

1. **Encryption**: BackendConfig is automatically encrypted before storage
2. **No Double Encryption**: If already encrypted (starts with `ENC:`), won't encrypt again
3. **Automatic Decryption**: Backend services receive decrypted config
4. **Sensitive Data**: Store passwords and connection strings safely in BackendConfig

## Testing

See [TESTING_BACKEND_CONNECTIONS.md](TESTING_BACKEND_CONNECTIONS.md) for connection testing examples.
