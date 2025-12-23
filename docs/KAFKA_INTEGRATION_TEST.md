# Kafka Integration Test - Manual Steps

This test validates that your webhook backend can successfully send messages to Kafka.

## Prerequisites

- ✅ Kafka running at localhost:9092
- ✅ API service running (check with `docker ps` or your deployment)
- ✅ Kafka UI available (optional, for verification)

## Test Steps

### Step 1: Find your API URL

Your API should be accessible at one of these:
- `http://localhost:5001` (if running locally)
- `https://localhost:7579` (if running locally with HTTPS)
- Your deployed URL

### Step 2: Create a test webhook

```bash
# Set your API URL
export API_URL="http://localhost:5001"

# Create webhook with Kafka backend
curl -X POST "$API_URL/api/webhooks" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Kafka Test Webhook",
    "slug": "kafka-test",
    "owner": "test-org",
    "project": "test-project",
    "backendType": 2,
    "backendConfig": "{\"BootstrapServers\":\"localhost:9092\",\"Topic\":\"webhooks-test\"}",
    "deliveryMode": 1,
    "status": 1
  }'
```

**Expected:** Returns a JSON response with the created webhook. Save the `id` field.

### Step 3: Test the Kafka connection

```bash
curl -X POST "$API_URL/api/webhooks/test-connection" \
  -H "Content-Type: application/json" \
  -d '{
    "backendType": 2,
    "backendConfig": "{\"BootstrapServers\":\"localhost:9092\",\"Topic\":\"webhooks-test\"}",
    "id": "00000000-0000-0000-0000-000000000000",
    "name": "Test"
  }'
```

**Expected response if Kafka is accessible:**
```json
{
  "success": true,
  "message": "Connection test successful"
}
```

**If you get `success: false`:**
- Check if Kafka is actually running: `docker ps | grep kafka`
- Check if the topic exists or if Kafka allows auto-creation
- Check API logs for detailed error messages

### Step 4: Send a test event

```bash
curl -X POST "$API_URL/api/webhook/test-org/test-project/kafka-test" \
  -H "Content-Type: application/json" \
  -d '{
    "eventType": "test.event",
    "timestamp": "'$(date -u +"%Y-%m-%dT%H:%M:%SZ")'",
    "data": {
      "message": "Test message to Kafka",
      "value": 12345
    }
  }' | jq
```

**Expected response:**
```json
{
  "id": "some-guid",
  "status": 2,        // 2 = Processed
  "subStatus": 1,     // 1 = Success
  "statusResultText": "Message delivered to partition 0, offset 123",
  "payload": "{...}"
}
```

**Key indicators:**
- ✅ `status: 2` means the event was processed
- ✅ `subStatus: 1` means it was successful
- ✅ `statusResultText` should show partition and offset numbers
- ❌ `subStatus: 2` means it failed (check `statusResultText` for error)

### Step 5: Verify in Kafka UI

1. **Open Kafka UI** (usually http://localhost:8080)
2. **Go to Topics** → Find `webhooks-test`
3. **Click on Messages** or **View Messages**
4. **Look for the latest message**

**What you should see:**
- **Key**: The webhook event ID (GUID)
- **Value**: Your JSON payload
- **Headers**:
  - `webhook-id`: Your webhook's ID
  - `webhook-name`: "Kafka Test Webhook"
  - `webhook-event-id`: The event ID
  - `timestamp`: ISO 8601 timestamp

### Step 6: Verify with kafkacat (alternative)

If you have kafkacat/kcat installed:

```bash
# Read the last message from the topic
kcat -C -b localhost:9092 -t webhooks-test -o -1 -c 1 \
  -f 'Key: %k\nValue: %s\nHeaders: %h\n\n'
```

### Step 7: Check API logs

Look for these log messages in your API logs:

```
info: Received webhook event for test-org/test-project/kafka-test
info: Found webhook {id} (Kafka Test Webhook), backend type: Kafka
info: Sending webhook event {event-id} to Kafka backend
info: KafkaBackend.SendAsync called for webhook {id}
info: Kafka config parsed: BootstrapServers=localhost:9092, Topic=webhooks-test
info: Creating Kafka producer for localhost:9092...
info: Successfully sent webhook event {id} to Kafka topic webhooks-test, partition 0, offset 123
info: Backend Kafka returned True for webhook event {id}
```

## Automated Test Script

Run the automated test:

```bash
cd /workspaces/webhooks-dotnet
export API_URL="http://localhost:5001"
./scripts/test-kafka-integration.sh
```

This script will:
1. Create a test webhook
2. Test the connection
3. Send a test event
4. Show you where to check in Kafka UI
5. Clean up the test webhook

## Troubleshooting

### Error: Connection test failed

**Check Kafka is running:**
```bash
docker ps | grep kafka
telnet localhost 9092
```

**Check Kafka logs:**
```bash
docker logs <kafka-container-name>
```

### Error: Topic doesn't exist

**Create the topic:**
```bash
docker exec <kafka-container> kafka-topics --create \
  --topic webhooks-test \
  --bootstrap-server localhost:9092
```

Or enable auto-topic creation in Kafka config.

### Error: Backend processing failed

**Check API logs:**
```bash
docker logs <api-container> | grep -A 5 -B 5 -i kafka
```

Look for the detailed error messages from the new logging.

### Messages not appearing in Kafka UI

1. **Refresh the page** - UI might be cached
2. **Check correct topic** - Make sure you're looking at `webhooks-test`
3. **Check topic has messages**:
   ```bash
   docker exec <kafka-container> kafka-run-class kafka.tools.GetOffsetShell \
     --broker-list localhost:9092 --topic webhooks-test
   ```
4. **Try reading with console consumer**:
   ```bash
   docker exec <kafka-container> kafka-console-consumer \
     --bootstrap-server localhost:9092 \
     --topic webhooks-test \
     --from-beginning \
     --max-messages 1
   ```

## Expected Results

After running the test successfully:

✅ Webhook created with Kafka backend configured
✅ Connection test passes
✅ Event sent to API returns status=2, subStatus=1
✅ Message visible in Kafka UI with correct key, value, and headers
✅ API logs show successful Kafka producer operation
