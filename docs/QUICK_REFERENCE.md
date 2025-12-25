# Webhook Backend Quick Reference

## Backend Types (enum values)

| Type | Value | Description | Config Example |
|------|-------|-------------|----------------|
| Database | 1 | Store in SQL Server (default) | `null` |
| Kafka | 2 | Send to Kafka topic | `{"BootstrapServers":"kafka:9092","Topic":"events"}` |
| RabbitMQ | 3 | Send to RabbitMQ queue | `{"HostName":"rabbit","QueueName":"webhooks"}` |
| AzureServiceBus | 4 | Send to Azure Service Bus | TBD |
| AWSSQS | 5 | Send to AWS SQS | TBD |
| Redis | 6 | Publish to Redis channel | TBD |
| Custom | 99 | Custom implementation | varies |

## Delivery Modes

| Mode | Value | Behavior |
|------|-------|----------|
| Synchronous | 1 | Wait for backend confirmation before returning |
| Asynchronous | 2 | Fire-and-forget, return immediately |

## API Endpoints

### Create Webhook with Backend
```http
POST /api/webhooks
Content-Type: application/json

{
  "name": "My Webhook",
  "slug": "my-webhook",
  "owner": "org-name",
  "project": "project-name",
  "status": 1,
  "backendType": 2,
  "backendConfig": "{\"BootstrapServers\":\"kafka:9092\",\"Topic\":\"events\"}",
  "deliveryMode": 1
}
```

### Send Webhook Payload
```http
POST /api/wes/{org}/{project}/{webhookSlug}
Content-Type: application/json

{
  "eventType": "user.created",
  "data": { ... }
}
```

## Database Schema

### Webhook Table (new fields)
- `BackendType` (int) - Backend type enum value
- `BackendConfig` (nvarchar(max)) - JSON config (SENSITIVE)
- `DeliveryMode` (int) - Delivery mode enum value
- `LastReceivedAt` (datetime2) - Last received timestamp

## Code Examples

### Adding a New Backend

```csharp
// 1. Create implementation
public class MyCustomBackend : IWebhookBackend
{
    public WebhookBackendType BackendType => WebhookBackendType.Custom;
    
    public async Task<WebhookBackendResult> SendAsync(
        Webhook webhook, 
        string payload, 
        Guid webhookEventId, 
        CancellationToken cancellationToken)
    {
        // Your implementation here
        return new WebhookBackendResult 
        { 
            Success = true, 
            Message = "Sent successfully" 
        };
    }
    
    public Task<bool> TestConnectionAsync(
        Webhook webhook, 
        CancellationToken cancellationToken)
    {
        // Test connection
        return Task.FromResult(true);
    }
}

// 2. Register in Program.cs
builder.Services.AddSingleton<IWebhookBackend, MyCustomBackend>();
```

### Using Backend in Tests

```csharp
// Mock backend
var mockBackend = new Mock<IWebhookBackend>();
mockBackend.Setup(b => b.BackendType).Returns(WebhookBackendType.Kafka);
mockBackend.Setup(b => b.SendAsync(...))
    .ReturnsAsync(new WebhookBackendResult { Success = true });

// Mock factory
var mockFactory = new Mock<IWebhookBackendFactory>();
mockFactory.Setup(f => f.GetBackend(It.IsAny<WebhookBackendType>()))
    .Returns(mockBackend.Object);

// Create controller with mocks
var controller = new WebhookEventsSubmissionController(
    context, 
    mockFactory.Object, 
    logger);
```

## Flow Diagram

```
Incoming Webhook Payload
         ↓
  Lookup Webhook Config
         ↓
  Update LastReceivedAt
         ↓
Save Metadata to Database (always)
         ↓
    Get Backend from Factory
         ↓
   +--------------------+
   |  DeliveryMode?    |
   +--------------------+
   |        |           |
Sync      Async
   |        |
   |    Fire background task
   |        ↓
   |    Return immediately
   |
Send to Backend
   ↓
Update Event Status
   ↓
Return Response
```

## Security Notes

⚠️ **NEVER expose `BackendConfig` in API responses**  
✅ Use `WebhookDto` which excludes sensitive config  
✅ Only `BackendType` is public information  
🔐 Consider encrypting `BackendConfig` at rest (future)

## Monitoring

Track these fields in `WebhookEvent` table:
- `Status` - New / Received / Processed
- `SubStatus` - Pending / Success / Failed / Retry / Skipped
- `StatusResultText` - Backend result message
- `CreatedAt` / `UpdatedAt` - Timestamps

Track in `Webhook` table:
- `LastReceivedAt` - Activity monitoring
