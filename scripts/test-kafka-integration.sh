#!/bin/bash

# Kafka Integration Test
# Tests that webhook backend can send messages to Kafka

set -e

API_URL="${API_URL:-http://localhost:5001}"
KAFKA_BOOTSTRAP="${KAFKA_BOOTSTRAP:-localhost:9092}"
TOPIC="${TOPIC:-webhooks-test}"

echo "=========================================="
echo "Kafka Integration Test"
echo "=========================================="
echo "API URL: $API_URL"
echo "Kafka: $KAFKA_BOOTSTRAP"
echo "Topic: $TOPIC"
echo ""

# Test 1: Create a webhook with Kafka backend
echo "Step 1: Creating webhook with Kafka backend..."
WEBHOOK_RESPONSE=$(curl -s -X POST "$API_URL/api/webhooks" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Integration Test Webhook",
    "slug": "integration-test-webhook",
    "owner": "test-org",
    "project": "test-project",
    "backendType": 2,
    "backendConfig": "{\"BootstrapServers\":\"'"$KAFKA_BOOTSTRAP"'\",\"Topic\":\"'"$TOPIC"'\"}",
    "deliveryMode": 1,
    "status": 1
  }')

WEBHOOK_ID=$(echo $WEBHOOK_RESPONSE | jq -r '.id')
echo "✓ Webhook created: $WEBHOOK_ID"
echo ""

# Test 2: Test connection
echo "Step 2: Testing Kafka connection..."
CONNECTION_TEST=$(curl -s -X POST "$API_URL/api/webhooks/test-connection" \
  -H "Content-Type: application/json" \
  -d '{
    "backendType": 2,
    "backendConfig": "{\"BootstrapServers\":\"'"$KAFKA_BOOTSTRAP"'\",\"Topic\":\"'"$TOPIC"'\"}",
    "id": "00000000-0000-0000-0000-000000000000",
    "name": "Test"
  }')

SUCCESS=$(echo $CONNECTION_TEST | jq -r '.success')
MESSAGE=$(echo $CONNECTION_TEST | jq -r '.message')

if [ "$SUCCESS" = "true" ]; then
    echo "✓ Connection test passed: $MESSAGE"
else
    echo "✗ Connection test failed: $MESSAGE"
    echo "Full response: $CONNECTION_TEST"
    exit 1
fi
echo ""

# Test 3: Send a webhook event
echo "Step 3: Sending test webhook event..."
TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
EVENT_RESPONSE=$(curl -s -X POST "$API_URL/api/webhook/test-org/test-project/integration-test-webhook" \
  -H "Content-Type: application/json" \
  -d '{
    "eventType": "integration.test",
    "timestamp": "'"$TIMESTAMP"'",
    "testId": "'"$(uuidgen)"'",
    "data": {
      "message": "Integration test message",
      "value": 12345,
      "timestamp": "'"$TIMESTAMP"'"
    }
  }')

EVENT_ID=$(echo $EVENT_RESPONSE | jq -r '.id')
EVENT_STATUS=$(echo $EVENT_RESPONSE | jq -r '.status')
EVENT_SUBSTATUS=$(echo $EVENT_RESPONSE | jq -r '.subStatus')
EVENT_RESULT=$(echo $EVENT_RESPONSE | jq -r '.statusResultText')

echo "Event ID: $EVENT_ID"
echo "Status: $EVENT_STATUS (2=Processed)"
echo "SubStatus: $EVENT_SUBSTATUS (1=Success, 2=Failed)"
echo "Result: $EVENT_RESULT"
echo ""

if [ "$EVENT_STATUS" = "2" ] && [ "$EVENT_SUBSTATUS" = "1" ]; then
    echo "✓ Event processed successfully!"
    echo ""
    echo "=========================================="
    echo "SUCCESS! Message sent to Kafka"
    echo "=========================================="
    echo ""
    echo "The webhook event was sent to Kafka topic '$TOPIC'"
    echo "Message details from response: $EVENT_RESULT"
    echo ""
    echo "📋 NEXT: Check your Kafka UI to verify the message arrived:"
    echo "   1. Open Kafka UI (usually http://localhost:8080)"
    echo "   2. Navigate to Topics → $TOPIC"
    echo "   3. Look for the most recent message"
    echo "   4. Check the message key matches: $EVENT_ID"
    echo "   5. Verify the message headers contain:"
    echo "      - webhook-id: $WEBHOOK_ID"
    echo "      - webhook-name: Integration Test Webhook"
    echo "      - webhook-event-id: $EVENT_ID"
    echo ""
    echo "📊 You can also verify using kafkacat:"
    echo "   kcat -C -b $KAFKA_BOOTSTRAP -t $TOPIC -f 'Key: %k\\nValue: %s\\n\\n' | tail -n 10"
    echo ""
else
    echo "✗ Event processing failed!"
    echo "Full response: $EVENT_RESPONSE"
    echo ""
    echo "Check API logs for errors:"
    echo "  docker logs <api-container> | grep -i kafka"
    exit 1
fi

# Cleanup
echo "Step 4: Cleaning up test webhook..."
curl -s -X DELETE "$API_URL/api/webhooks/$WEBHOOK_ID" > /dev/null
echo "✓ Test webhook deleted"
echo ""
echo "=========================================="
echo "Integration test completed successfully!"
echo "=========================================="
