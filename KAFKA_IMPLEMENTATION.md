# Kafka Backend Implementation

## Overview

The Kafka backend has been fully implemented using the **Confluent.Kafka** library to actually send webhook events to Kafka topics.

## What Was Implemented

### 1. **Real Kafka Producer** ([KafkaBackend.cs](webhooks.SharedModels/src/backends/KafkaBackend.cs))

**Previous Implementation:**
- Just logged "would send" messages
- Simulated delays
- No actual Kafka interaction

**New Implementation:**
- Creates real Kafka producer with `Confluent.Kafka`
- Sends messages to configured topic
- Includes message headers with metadata:
  - `webhook-id`: The webhook's unique ID
  - `webhook-name`: The webhook name
  - `webhook-event-id`: The event ID
  - `timestamp`: UTC timestamp of the event
- Returns partition and offset information
- Handles Kafka-specific errors with detailed logging

### 2. **Connection Testing**

**Previous Implementation:**
- Just returned `true` after delay
- No actual validation

**New Implementation:**
- Uses Kafka AdminClient to fetch cluster metadata
- Verifies broker connectivity
- Checks if specified topic exists
- Logs cluster information (broker count, available topics)
- Returns detailed error messages on failure

### 3. **Configuration**

**Producer Settings:**
```csharp
- BootstrapServers: from BackendConfig
- ClientId: "webhooks-{webhook-id}"
- Acks: Leader (waits for leader acknowledgment)
- MessageTimeoutMs: 10000 (10 seconds)
- RequestTimeoutMs: 5000 (5 seconds)
- MessageSendMaxRetries: 3
- RetryBackoffMs: 100
```

### 4. **Message Format**

Messages sent to Kafka have:
- **Key**: `webhookEventId` (as string) - for partitioning
- **Value**: The webhook payload (JSON string)
- **Headers**: Metadata about the webhook and event

### 5. **Dependencies Added**

**Package**: `Confluent.Kafka` v2.6.1

Added to [webhooks.SharedModels.csproj](webhooks.SharedModels/webhooks.SharedModels.csproj):
```xml
<PackageReference Include="Confluent.Kafka" Version="2.6.1" />
```

## Usage

### Creating a Kafka Webhook

```json
POST /api/webhooks
{
  "name": "My Kafka Webhook",
  "slug": "my-kafka-webhook",
  "backendType": 2,
  "backendConfig": "{\"BootstrapServers\":\"10.10.100.93:9092\",\"Topic\":\"webhooks\"}",
  "owner": "my-org",
  "project": "my-project"
}
```

### Testing Connection

```json
POST /api/webhooks/test-connection
{
  "backendType": 2,
  "backendConfig": "{\"BootstrapServers\":\"10.10.100.93:9092\",\"Topic\":\"webhooks\"}"
}
```

### Triggering an Event

```json
POST /api/webhookevents/{owner}/{project}/{slug}
{
  "eventType": "order.created",
  "data": {
    "orderId": "12345",
    "amount": 99.99
  }
}
```

The message will be sent to Kafka with:
- **Topic**: `webhooks`
- **Key**: The webhook event GUID
- **Value**: The JSON payload
- **Headers**: Metadata about the webhook

## Monitoring

### Successful Send Log

```
Successfully sent webhook event {guid} to Kafka topic webhooks at 10.10.100.93:9092, partition 0, offset 1234
```

### Connection Test Log

```
Successfully connected to Kafka at 10.10.100.93:9092. Cluster has 3 broker(s), 15 topic(s)
```

### Warning if Topic Doesn't Exist

```
Topic webhooks does not exist on Kafka cluster. Available topics: topic1, topic2, topic3
```

## Consuming Messages from Kafka

Your Kafka consumers can read messages from the configured topic. Each message will have:

### Headers
- `webhook-id`: The webhook's ID
- `webhook-name`: The webhook name
- `webhook-event-id`: The event ID for tracking
- `timestamp`: ISO 8601 timestamp

### Key
- The `webhookEventId` GUID as a string

### Value
- The complete JSON payload that was posted to the webhook

### Example Consumer (using kafkacat/kcat)

```bash
# Read all messages from the webhooks topic
kcat -C -b 10.10.100.93:9092 -t webhooks

# Read with headers
kcat -C -b 10.10.100.93:9092 -t webhooks -f 'Headers: %h\nKey: %k\nValue: %s\n\n'
```

### Example Consumer (Python with confluent-kafka)

```python
from confluent_kafka import Consumer

consumer = Consumer({
    'bootstrap.servers': '10.10.100.93:9092',
    'group.id': 'webhook-consumer',
    'auto.offset.reset': 'earliest'
})

consumer.subscribe(['webhooks'])

while True:
    msg = consumer.poll(1.0)
    if msg is None:
        continue
    
    # Read headers
    headers = {k: v.decode('utf-8') for k, v in msg.headers()}
    webhook_id = headers.get('webhook-id')
    webhook_name = headers.get('webhook-name')
    timestamp = headers.get('timestamp')
    
    # Read message
    key = msg.key().decode('utf-8')  # webhook event ID
    value = msg.value().decode('utf-8')  # JSON payload
    
    print(f"Received from webhook '{webhook_name}' ({webhook_id})")
    print(f"Event ID: {key}")
    print(f"Payload: {value}")
```

## Error Handling

### Kafka Connection Errors

If Kafka is unreachable:
```json
{
  "success": false,
  "message": "Kafka error: Broker: Not available"
}
```

### Invalid Configuration

If BootstrapServers or Topic is missing:
```json
{
  "success": false,
  "message": "Kafka BootstrapServers not configured"
}
```

### Invalid JSON Format

If BackendConfig is not valid JSON:
```json
{
  "success": false,
  "message": "Invalid BackendConfig format. Kafka requires JSON format: {\"BootstrapServers\":\"host:port\",\"Topic\":\"topic-name\"}"
}
```

## Testing

Unit tests have been updated to work with the real implementation:
- Tests that don't require Kafka use mocked backends
- Integration tests would require a running Kafka instance
- The implementation gracefully handles errors when Kafka is unavailable

## Next Steps

For production deployments:

1. **Kafka Cluster Setup**: Ensure your Kafka cluster is running and accessible
2. **Topic Creation**: Pre-create topics or enable auto-creation
3. **Security**: Add SASL/SSL configuration if needed
4. **Monitoring**: Set up Kafka monitoring dashboards
5. **Consumer Groups**: Set up consumer applications to process webhook events
6. **Error Handling**: Monitor failed deliveries and set up alerting

## See Also

- [Backend Configuration Format](docs/BACKEND_CONFIG_FORMAT.md) - Configuration format reference
- [Testing Backend Connections](docs/TESTING_BACKEND_CONNECTIONS.md) - Testing guide
- [Webhook Backends](docs/WEBHOOK_BACKENDS.md) - Overview of all backends
