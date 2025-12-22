#!/bin/bash
# Example: Create webhooks with different backends and test them

API_BASE="http://localhost:5431/api"

echo "=== Creating Webhooks with Different Backends ==="
echo ""

# 1. Create a Database webhook (default)
echo "1. Creating Database webhook..."
curl -X POST "$API_BASE/webhooks" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Database Webhook",
    "slug": "db-webhook",
    "owner": "demo-org",
    "project": "demo-project",
    "status": 1,
    "backendType": 1,
    "deliveryMode": 1
  }'
echo -e "\n"

# 2. Create a Kafka webhook
echo "2. Creating Kafka webhook..."
curl -X POST "$API_BASE/webhooks" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Kafka Webhook",
    "slug": "kafka-webhook",
    "owner": "demo-org",
    "project": "demo-project",
    "status": 1,
    "backendType": 2,
    "backendConfig": "{\"BootstrapServers\":\"localhost:9092\",\"Topic\":\"webhook-events\"}",
    "deliveryMode": 2
  }'
echo -e "\n"

# 3. Create a RabbitMQ webhook
echo "3. Creating RabbitMQ webhook..."
curl -X POST "$API_BASE/webhooks" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "RabbitMQ Webhook",
    "slug": "rabbitmq-webhook",
    "owner": "demo-org",
    "project": "demo-project",
    "status": 1,
    "backendType": 3,
    "backendConfig": "{\"HostName\":\"localhost\",\"QueueName\":\"webhooks\",\"UserName\":\"guest\",\"Password\":\"guest\"}",
    "deliveryMode": 1
  }'
echo -e "\n"

echo "=== Sending Test Payloads ==="
echo ""

# Send to Database webhook
echo "1. Sending to Database webhook..."
curl -X POST "$API_BASE/wes/demo-org/demo-project/db-webhook" \
  -H "Content-Type: application/json" \
  -d '{
    "eventType": "user.created",
    "userId": "12345",
    "timestamp": "2025-12-21T10:00:00Z",
    "data": {
      "email": "user@example.com",
      "name": "John Doe"
    }
  }'
echo -e "\n"

# Send to Kafka webhook
echo "2. Sending to Kafka webhook (async)..."
curl -X POST "$API_BASE/wes/demo-org/demo-project/kafka-webhook" \
  -H "Content-Type: application/json" \
  -d '{
    "eventType": "order.placed",
    "orderId": "ORD-789",
    "timestamp": "2025-12-21T10:05:00Z",
    "data": {
      "amount": 99.99,
      "currency": "USD"
    }
  }'
echo -e "\n"

# Send to RabbitMQ webhook
echo "3. Sending to RabbitMQ webhook (sync)..."
curl -X POST "$API_BASE/wes/demo-org/demo-project/rabbitmq-webhook" \
  -H "Content-Type: application/json" \
  -d '{
    "eventType": "payment.processed",
    "paymentId": "PAY-456",
    "timestamp": "2025-12-21T10:10:00Z",
    "data": {
      "status": "success",
      "amount": 99.99
    }
  }'
echo -e "\n"

echo "=== Listing Webhooks ==="
curl -X GET "$API_BASE/webhooks"
echo -e "\n"

echo ""
echo "=== Backend Types Reference ==="
echo "1 = Database (default)"
echo "2 = Kafka"
echo "3 = RabbitMQ"
echo "4 = AzureServiceBus"
echo "5 = AWSSQS"
echo "6 = Redis"
echo "99 = Custom"
echo ""
echo "=== Delivery Modes ==="
echo "1 = Synchronous (wait for confirmation)"
echo "2 = Asynchronous (fire-and-forget)"
