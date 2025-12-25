# Webhook Backend Architecture

## Overview

The webhook system now supports multiple backend storage/messaging systems beyond the database. Each webhook can be configured with its own backend type, allowing flexibility in how webhook payloads are processed and stored.

## Key Features

- **Multiple Backend Support**: Database, Kafka, RabbitMQ, Azure Service Bus, AWS SQS, Redis, and Custom backends
- **Extensible Design**: Easy to add new backend implementations via the `IWebhookBackend` interface
- **Per-Webhook Configuration**: Each webhook has its own backend configuration
- **Delivery Modes**: Synchronous (wait for confirmation) or Asynchronous (fire-and-forget)
- **Metadata Tracking**: Database always stores metadata regardless of backend type
- **Backward Compatible**: Existing webhooks default to Database backend

## Architecture

### Core Components

1. **`IWebhookBackend` Interface** ([backends/IWebhookBackend.cs](webhooks.SharedModels/src/backends/IWebhookBackend.cs))
   - Defines the contract for all backend implementations
   - Methods: `SendAsync()`, `TestConnectionAsync()`
   - Returns `WebhookBackendResult` with success status and metadata

2. **Backend Implementations**
   - `DatabaseBackend` ([backends/DatabaseBackend.cs](webhooks.SharedModels/src/backends/DatabaseBackend.cs)) - Default/current behavior
   - `KafkaBackend` ([backends/KafkaBackend.cs](webhooks.SharedModels/src/backends/KafkaBackend.cs)) - Send to Kafka topics
   - `RabbitMQBackend` ([backends/RabbitMQBackend.cs](webhooks.SharedModels/src/backends/RabbitMQBackend.cs)) - Send to RabbitMQ queues

3. **`WebhookBackendFactory`** ([backends/WebhookBackendFactory.cs](webhooks.SharedModels/src/backends/WebhookBackendFactory.cs))
   - Factory pattern for instantiating the correct backend based on webhook configuration
   - Caches backend instances for performance
   - Falls back to Database backend if specified backend is unavailable

4. **Enhanced Webhook Model** ([models/Webhook.cs](webhooks.SharedModels/src/models/Webhook.cs))
   - `BackendType` (enum) - Type of backend to use
   - `BackendConfig` (JSON string) - Backend-specific configuration (SENSITIVE - not exposed in DTOs)
   - `DeliveryMode` (enum) - Synchronous or Asynchronous
   - `LastReceivedAt` (DateTime) - Timestamp of last received webhook

### Enums

```csharp
public enum WebhookBackendType
{
    Database = 1,
    Kafka = 2,
    RabbitMQ = 3,
    AzureServiceBus = 4,
    AWSSQS = 5,
    Redis = 6,
    Custom = 99
}

public enum WebhookDeliveryMode
{
    Synchronous = 1,      // Wait for backend confirmation
    Asynchronous = 2      // Fire-and-forget
}
```

## Webhook Submission Flow

When a webhook payload is received at `POST /api/wes/{org}/{project}/{webhookSlug}`:

1. **Lookup Webhook**: Find webhook by org/project/slug
2. **Update Metadata**: Set `LastReceivedAt` timestamp
3. **Create WebhookEvent**: Save event record to database (always, regardless of backend)
4. **Get Backend**: Factory resolves the appropriate backend implementation
5. **Send to Backend**:
   - **Synchronous Mode**: Wait for backend to process, update event status
   - **Asynchronous Mode**: Fire background task, return immediately
6. **Return Response**: Return created webhook event to caller

## Backend Configuration

### Database Backend
```json
{
  "BackendType": 1,
  "BackendConfig": null,
  "DeliveryMode": 1
}
```

### Kafka Backend
```json
{
  "BackendType": 2,
  "BackendConfig": "{\"BootstrapServers\":\"localhost:9092\",\"Topic\":\"webhook-events\"}",
  "DeliveryMode": 1
}
```

### RabbitMQ Backend
```json
{
  "BackendType": 3,
  "BackendConfig": "{\"HostName\":\"localhost\",\"QueueName\":\"webhooks\",\"UserName\":\"guest\",\"Password\":\"guest\"}",
  "DeliveryMode": 2
}
```

## Security Considerations

- **`BackendConfig` is SENSITIVE**: Contains connection strings, credentials, etc.
- **Encrypted at rest**: BackendConfig is encrypted using AES-256 (see [Backend Security](BACKEND_SECURITY.md))
- **Never exposed via API**: Use `WebhookDto` when exposing webhooks - it excludes `BackendConfig`
- Only `BackendType` is considered non-secret
- **Test before saving**: Use the Test Connection feature to validate configuration (see [Testing Backend Connections](TESTING_BACKEND_CONNECTIONS.md))

## Database Schema Changes

Migration: [20_add_backend_config.sql](webhooks.StorageMigrations/migrations/20_add_backend_config.sql)

New columns on `Webhooks` table:
- `BackendType` (int, default: 1) - FK to WebhookBackendType table
- `BackendConfig` (nvarchar(max), nullable) - JSON configuration
- `DeliveryMode` (int, default: 1) - FK to WebhookDeliveryMode table
- `LastReceivedAt` (datetime2, nullable) - Last received timestamp

New reference tables:
- `WebhookBackendType` - Backend type lookup
- `WebhookDeliveryMode` - Delivery mode lookup

## Adding a New Backend

To add support for a new backend (e.g., Azure Service Bus):

1. **Add Backend Type** to `WebhookBackendType` enum:
   ```csharp
   AzureServiceBus = 4
   ```

2. **Create Implementation**:
   ```csharp
   public class AzureServiceBusBackend : IWebhookBackend
   {
       public WebhookBackendType BackendType => WebhookBackendType.AzureServiceBus;
       
       public async Task<WebhookBackendResult> SendAsync(
           Webhook webhook, 
           string payload, 
           Guid webhookEventId, 
           CancellationToken cancellationToken)
       {
           // Parse BackendConfig JSON
           // Connect to Azure Service Bus
           // Send message
           // Return result
       }
       
       public async Task<bool> TestConnectionAsync(
           Webhook webhook, 
           CancellationToken cancellationToken)
       {
           // Test connection
       }
   }
   ```

3. **Register in DI** ([webhooks.ApiService/Program.cs](webhooks.ApiService/Program.cs)):
   ```csharp
   builder.Services.AddSingleton<IWebhookBackend, AzureServiceBusBackend>();
   ```

4. **Update Migration**: Add entry to `WebhookBackendType` reference table

## Testing

Test files:
- [WebhookBackendTests.cs](webhooks.Tests/WebhookBackendTests.cs) - Unit tests for backend functionality
- [WebhookSubmissionEventAPITests.cs](webhooks.Tests/WebhookSubmissionEventAPITests.cs) - API tests with mocked backends
- [WebhookE2ETests.cs](webhooks.Tests/WebhookE2ETests.cs) - End-to-end tests

Mock backends using Moq:
```csharp
var mockBackend = new Mock<IWebhookBackend>();
mockBackend.Setup(b => b.BackendType).Returns(WebhookBackendType.Kafka);
mockBackend.Setup(b => b.SendAsync(...))
    .ReturnsAsync(new WebhookBackendResult { Success = true });

var mockFactory = new Mock<IWebhookBackendFactory>();
mockFactory.Setup(f => f.GetBackend(WebhookBackendType.Kafka))
    .Returns(mockBackend.Object);
```

## Performance Considerations

- **Backend Factory Caching**: Backend instances are cached for reuse
- **Async Processing**: Use `DeliveryMode.Asynchronous` for long-running backends
- **Database Always Writes**: Metadata is always written to DB for audit trail
- **Connection Pooling**: Backend implementations should implement connection pooling

## Future Enhancements

1. **Retry Logic**: Automatic retry for failed backend sends
2. **Circuit Breaker**: Prevent cascading failures when backend is down
3. **Config Encryption**: Encrypt BackendConfig at rest in database
4. **Backend Health Monitoring**: Track backend availability and performance
5. **Bulk Operations**: Batch multiple webhook events for efficiency
6. **Dead Letter Queue**: Store failed messages for later processing
7. **Rate Limiting**: Per-backend rate limiting configuration

## Example Usage

### Creating a Kafka Webhook

```csharp
POST /api/webhooks
{
  "Name": "My Kafka Webhook",
  "Slug": "my-kafka-webhook",
  "Owner": "acme-corp",
  "Project": "analytics",
  "Status": 1,
  "BackendType": 2,
  "BackendConfig": "{\"BootstrapServers\":\"kafka:9092\",\"Topic\":\"events\"}",
  "DeliveryMode": 2
}
```

### Sending to Kafka Webhook

```bash
POST /api/wes/acme-corp/analytics/my-kafka-webhook
{
  "eventType": "user.signup",
  "userId": "12345",
  "timestamp": "2025-12-21T10:00:00Z"
}
```

The payload will be:
1. Saved to `WebhookEvents` table as metadata
2. Sent to Kafka topic `events` asynchronously
3. Response returned immediately (async mode)

## Monitoring

Track webhook backend performance:
- `WebhookEvent.Status` - Current processing status
- `WebhookEvent.SubStatus` - Success/Failed/Retry/Skipped
- `WebhookEvent.StatusResultText` - Backend result message
- `Webhook.LastReceivedAt` - Activity tracking
