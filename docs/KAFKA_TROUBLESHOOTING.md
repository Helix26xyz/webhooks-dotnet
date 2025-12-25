# Kafka Webhook Troubleshooting Guide

## Problem: No logs in API server and no messages in Kafka

### Step 1: Verify the webhook exists

```bash
# List all webhooks
curl http://your-api-url/api/webhooks

# Check your specific webhook
curl http://your-api-url/api/webhooks | jq '.[] | select(.backendType == 2)'
```

**Expected output:**
```json
{
  "id": "...",
  "name": "My Kafka Webhook",
  "slug": "my-kafka-webhook",
  "backendType": 2,
  "deliveryMode": 1,
  "status": 1,
  "owner": "my-org",
  "project": "my-project"
}
```

**Check:**
- ✅ `backendType` should be `2` (Kafka)
- ✅ `status` should be `1` (Enabled)
- ✅ `backendConfig` should NOT be visible (it's encrypted)

---

### Step 2: Check log level configuration

The new logs are at **Information** level. Check your `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "webhooks.SharedModels.backends": "Information"
    }
  }
}
```

If logs are set to "Warning" or higher, you won't see the Information logs.

---

### Step 3: Trigger the webhook with verbose output

```bash
curl -X POST http://your-api-url/api/webhook/my-org/my-project/my-kafka-webhook \
  -H "Content-Type: application/json" \
  -d '{"test": "data"}' \
  -v
```

**Expected HTTP response:**
- Status: `201 Created`
- Body: A webhook event object with `status` and `subStatus`

**Check the response:**
```json
{
  "id": "...",
  "status": 2,         // 2 = Processed
  "subStatus": 1,      // 1 = Success, 2 = Failed
  "statusResultText": "Message delivered to partition 0, offset 123",
  "payload": "{\"test\":\"data\"}"
}
```

If `subStatus` is `2` (Failed), check `statusResultText` for the error message.

---

### Step 4: Check the expected logs

With the new changes, you should see these logs in order:

#### 1. Request received
```
info: webhooks.ApiService.src.WebhookEventsSubmissionController[0]
      Received webhook event for my-org/my-project/my-kafka-webhook
```

#### 2. Webhook found
```
info: webhooks.ApiService.src.WebhookEventsSubmissionController[0]
      Found webhook abc-123 (My Kafka Webhook), backend type: Kafka, delivery mode: Synchronous
```

#### 3. Backend call initiated
```
info: webhooks.ApiService.src.WebhookEventsSubmissionController[0]
      Sending webhook event xyz-456 to Kafka backend for webhook abc-123 (My Kafka Webhook)
```

#### 4. KafkaBackend called
```
info: webhooks.SharedModels.backends.KafkaBackend[0]
      KafkaBackend.SendAsync called for webhook abc-123 (My Kafka Webhook), event xyz-456
```

#### 5. Config parsed
```
info: webhooks.SharedModels.backends.KafkaBackend[0]
      Kafka config parsed: BootstrapServers=10.10.100.93:9092, Topic=webhooks
```

#### 6. Producer created
```
info: webhooks.SharedModels.backends.KafkaBackend[0]
      Creating Kafka producer for 10.10.100.93:9092...
```

#### 7. Message sent
```
info: webhooks.SharedModels.backends.KafkaBackend[0]
      Successfully sent webhook event xyz-456 to Kafka topic webhooks at 10.10.100.93:9092, partition 0, offset 123
```

#### 8. Backend result
```
info: webhooks.ApiService.src.WebhookEventsSubmissionController[0]
      Backend Kafka returned True for webhook event xyz-456: Message delivered to partition 0, offset 123
```

---

### Step 5: If you see NO logs at all

**Possible causes:**

1. **Wrong endpoint** - Make sure you're calling:
   ```
   POST /api/webhook/{owner}/{project}/{slug}
   ```
   NOT:
   ```
   POST /api/webhooks  (this creates a webhook, doesn't trigger one)
   ```

2. **Webhook not found** - If the webhook doesn't exist or is disabled:
   ```
   warn: webhooks.ApiService.src.WebhookEventsSubmissionController[0]
         Webhook not found or disabled: my-org/my-project/my-kafka-webhook
   ```

3. **Old code still running** - Redeploy with the new changes

---

### Step 6: If you see logs but no Kafka messages

**Check logs for errors:**

#### Error: Config parsing failed
```
fail: webhooks.SharedModels.backends.KafkaBackend[0]
      Error parsing Kafka backend config. Config preview (first 20 chars): ...
```
**Solution:** The BackendConfig is malformed. Check it's valid JSON.

#### Error: Kafka connection failed
```
fail: webhooks.SharedModels.backends.KafkaBackend[0]
      Kafka producer error sending webhook event xyz-456. Error: Broker: Not available
```
**Solution:** Kafka is not reachable. Check:
- Kafka is running: `telnet 10.10.100.93 9092`
- Network connectivity from the API pod/container to Kafka
- Firewall rules

#### Error: Topic doesn't exist
```
fail: webhooks.SharedModels.backends.KafkaBackend[0]
      Kafka producer error sending webhook event xyz-456. Error: Unknown topic or partition
```
**Solution:** Create the topic:
```bash
kafka-topics --create --topic webhooks --bootstrap-server 10.10.100.93:9092
```

---

### Step 7: Verify messages in Kafka

```bash
# Using kcat/kafkacat
kcat -C -b 10.10.100.93:9092 -t webhooks -f 'Key: %k\nValue: %s\n\n'

# Using kafka console consumer
kafka-console-consumer --bootstrap-server 10.10.100.93:9092 \
  --topic webhooks \
  --from-beginning \
  --property print.key=true
```

---

### Step 8: Check webhook event in database

Query the database to see webhook events:

```sql
SELECT 
    Id,
    Status,      -- 1=New, 2=Processed
    SubStatus,   -- 1=Success, 2=Failed
    StatusResultText,
    Payload,
    CreatedAt
FROM WebhookEvents
WHERE WebhookId = '<your-webhook-id>'
ORDER BY CreatedAt DESC;
```

---

## Quick Diagnostic Commands

### Check if API is receiving requests
```bash
# Check API logs for any recent activity
kubectl logs deployment/apiservice --tail=100 | grep "Received webhook event"
```

### Check if Kafka backend is being called
```bash
kubectl logs deployment/apiservice --tail=100 | grep "KafkaBackend.SendAsync"
```

### Check for any Kafka errors
```bash
kubectl logs deployment/apiservice --tail=100 | grep "Kafka" | grep -i error
```

### Verify Kafka connectivity from pod
```bash
# Get a shell in the API pod
kubectl exec -it deployment/apiservice -- /bin/bash

# Test Kafka connectivity
telnet 10.10.100.93 9092

# Or use curl if available
curl telnet://10.10.100.93:9092
```

---

## Common Issues

### Issue: "Webhook not found or disabled"
**Cause:** The owner/project/slug doesn't match any enabled webhook
**Fix:** Check webhook slug, owner, and project match exactly

### Issue: "Backend processing failed"
**Cause:** Exception during backend execution
**Fix:** Check full error in logs, look for stack trace

### Issue: "Kafka error: Broker: Not available"
**Cause:** Can't connect to Kafka
**Fix:** Verify Kafka is running and accessible from the API pod

### Issue: "Kafka config parsed: BootstrapServers=(null)"
**Cause:** BackendConfig is empty or invalid JSON
**Fix:** Update webhook with valid BackendConfig:
```json
{"BootstrapServers":"10.10.100.93:9092","Topic":"webhooks"}
```

---

## Testing Script

Use the included test script:
```bash
cd /workspaces/webhooks-dotnet
chmod +x scripts/test-kafka-webhook.sh

# Set your values
export API_URL=http://your-api-url
export OWNER=my-org
export PROJECT=my-project
export SLUG=my-kafka-webhook

./scripts/test-kafka-webhook.sh
```

This will show you exactly what logs to expect.
