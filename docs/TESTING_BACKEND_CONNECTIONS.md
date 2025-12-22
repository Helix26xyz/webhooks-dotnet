# Testing Backend Connections

## Overview

The webhook creation form now includes a **Test Connection** button that validates backend configuration before saving. This ensures connection strings are correct and the backend is reachable.

## Features

### Test Connection Button
- Located in the webhook creation form (Blazor UI)
- Tests backend connectivity without saving the webhook
- Provides real-time feedback on connection status
- Works with all backend types (Database, Kafka, RabbitMQ)

### Backend Configuration Fields

The form now includes:
1. **Backend Type** dropdown (Database/Kafka/RabbitMQ)
2. **Connection String** input with context-aware placeholders
3. **Delivery Mode** selector (Synchronous/Asynchronous)
4. **Test Connection** button with loading state

## Usage

### Via Web UI

1. Click **"Add New Webhook"** button
2. Fill in webhook details (Name, Slug, Owner, Project)
3. Select **Backend Type** from dropdown
4. Enter **Connection String** (placeholders help with format)
5. Click **Test Connection** button
6. Wait for validation result:
   - ✓ Green text = Connection successful
   - ✗ Red text = Connection failed with error details

### Connection String Examples

**Kafka:**
```
localhost:9092
```

**RabbitMQ:**
```
amqp://guest:guest@localhost:5672/
```

**Database:**
```
Leave empty to use default database
```

### Via API

Test a connection programmatically:

```bash
curl -X POST http://localhost:5001/api/webhooks/test-connection \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Test Webhook",
    "backendType": "Kafka",
    "backendConfig": "localhost:9092",
    "deliveryMode": "Asynchronous"
  }'
```

Response:
```json
{
  "isSuccess": true,
  "message": "Kafka connection test successful",
  "deliveredAt": "2025-12-22T10:30:00Z"
}
```

## Implementation Details

### Frontend (Blazor)
- [webhooks.Web/Components/Pages/WebhooksList.razor](../webhooks.Web/Components/Pages/WebhooksList.razor)
- `TestConnection()` method calls API and displays result
- Shows spinner during test
- Context-aware placeholders based on backend type

### API Client
- [webhooks.SharedModels/src/clients/WebhookApiClient.cs](../webhooks.SharedModels/src/clients/WebhookApiClient.cs)
- `TestBackendConnectionAsync()` method sends test request
- Returns `WebhookBackendResult` with success status and message

### API Endpoint
- [webhooks.ApiService/src/webhooks/WebhooksController.cs](../webhooks.ApiService/src/webhooks/WebhooksController.cs)
- `POST /api/webhooks/test-connection` endpoint
- Encrypts config (mimics save behavior)
- Uses `IWebhookBackendFactory` to get correct backend
- Calls `backend.TestConnectionAsync()`

### Backend Implementations
Each backend implements `TestConnectionAsync()`:
- **DatabaseBackend**: Validates database connection
- **KafkaBackend**: Connects to Kafka broker
- **RabbitMQBackend**: Validates AMQP connection

## Security Notes

- Connection strings are **encrypted** before testing (same as save)
- Test endpoint does **not persist** any data to database
- Failed tests provide error details for debugging
- Connection strings are **never exposed** in API responses

## Testing with Docker Compose

Start backend services locally:

```bash
docker-compose up -d kafka rabbitmq

# Test Kafka connection
# Use: localhost:9092

# Test RabbitMQ connection  
# Use: amqp://guest:guest@localhost:5672/

# View RabbitMQ Management UI
$BROWSER http://localhost:15672
```

## Troubleshooting

### "Connection timeout" error
- Ensure backend service is running (`docker-compose ps`)
- Check firewall rules for ports 9092 (Kafka) or 5672 (RabbitMQ)
- Verify connection string format

### "Authentication failed" error
- Check username/password in connection string
- RabbitMQ default: `guest/guest`
- Ensure credentials match backend configuration

### "Backend not found" error
- Verify backend type is spelled correctly (case-sensitive)
- Supported values: `Database`, `Kafka`, `RabbitMQ`

## Future Enhancements

- [ ] Test connection on webhook edit (not just create)
- [ ] Connection pooling test (verify multiple connections work)
- [ ] Advanced validation (topic/queue existence, permissions)
- [ ] Save last successful test timestamp
- [ ] Retry logic testing (simulate failures)
