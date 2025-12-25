# Multi-Backend Webhook Support

## Overview
Added support for multiple backend types (Kafka, RabbitMQ, Database, etc.) for webhook payload storage/delivery. Each webhook can be configured with its own backend while the database always maintains metadata for audit trails.

## Key Features
- ✅ Multiple backend types: Database, Kafka, RabbitMQ, Azure Service Bus, AWS SQS, Redis
- ✅ Extensible design via `IWebhookBackend` interface
- ✅ Per-webhook backend configuration
- ✅ Synchronous and asynchronous delivery modes
- ✅ Database always tracks metadata
- ✅ Backward compatible (existing webhooks use Database backend)
- ✅ All tests passing (15/15)

## Changes

### New Files
- **Backend Infrastructure**
  - `webhooks.SharedModels/src/backends/IWebhookBackend.cs`
  - `webhooks.SharedModels/src/backends/DatabaseBackend.cs`
  - `webhooks.SharedModels/src/backends/KafkaBackend.cs`
  - `webhooks.SharedModels/src/backends/RabbitMQBackend.cs`
  - `webhooks.SharedModels/src/backends/WebhookBackendFactory.cs`

- **Models**
  - `webhooks.SharedModels/src/models/WebhookDto.cs`

- **Database**
  - `webhooks.StorageMigrations/migrations/20_add_backend_config.sql`

- **Tests**
  - `webhooks.Tests/WebhookBackendTests.cs`

- **Documentation**
  - `docs/WEBHOOK_BACKENDS.md`
  - `docs/QUICK_REFERENCE.md`
  - `examples/webhook-backend-demo.sh`
  - `IMPLEMENTATION_SUMMARY.md`

### Modified Files
- `webhooks.SharedModels/src/models/Webhook.cs` - Added backend fields
- `webhooks.ApiService/src/webhooks/WebhookEventSubmissionController.cs` - Backend integration
- `webhooks.ApiService/Program.cs` - Service registration
- `webhooks.Tests/WebhookSubmissionEventAPITests.cs` - Updated tests
- `webhooks.Tests/WebhookE2ETests.cs` - Updated tests

## Database Schema
Added to `Webhooks` table:
- `BackendType` (int, default: 1) - Type of backend
- `BackendConfig` (nvarchar(max), nullable) - JSON configuration (sensitive)
- `DeliveryMode` (int, default: 1) - Sync or async delivery
- `LastReceivedAt` (datetime2, nullable) - Activity tracking

## Usage Example

Create a Kafka webhook:
```json
POST /api/webhooks
{
  "name": "Kafka Webhook",
  "slug": "kafka-webhook",
  "owner": "acme",
  "project": "analytics",
  "backendType": 2,
  "backendConfig": "{\"BootstrapServers\":\"kafka:9092\",\"Topic\":\"events\"}",
  "deliveryMode": 2
}
```

Send webhook payload:
```bash
POST /api/wes/acme/analytics/kafka-webhook
{
  "eventType": "user.signup",
  "userId": "12345"
}
```

## Testing
```bash
make test
# All 15 tests pass
```

## Documentation
See `docs/WEBHOOK_BACKENDS.md` for complete architecture documentation.
