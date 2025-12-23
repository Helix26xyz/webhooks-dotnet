#!/bin/bash

# Script to run Kafka integration tests that require a running Kafka instance
# These tests are skipped by default but can be run with this script

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${YELLOW}Kafka Integration Test Runner${NC}"
echo "=============================="
echo ""

# Check if Kafka is running
echo "Checking if Kafka is available at localhost:9092..."
if timeout 5 bash -c 'cat < /dev/null > /dev/tcp/localhost/9092' 2>/dev/null; then
    echo -e "${GREEN}✓ Kafka is reachable${NC}"
else
    echo -e "${RED}✗ Cannot reach Kafka at localhost:9092${NC}"
    echo ""
    echo "Please ensure Kafka is running. You can start it with:"
    echo "  docker run -d --name kafka -p 9092:9092 apache/kafka:latest"
    exit 1
fi

echo ""
echo "Running integration tests that send real messages to Kafka..."
echo ""

# Run the tests that require Kafka
cd /workspaces/webhooks-dotnet
dotnet test \
    --filter "FullyQualifiedName~KafkaIntegrationTests" \
    --logger "console;verbosity=detailed" \
    -- RunConfiguration.TestSessionTimeout=60000

TEST_EXIT_CODE=$?

echo ""
if [ $TEST_EXIT_CODE -eq 0 ]; then
    echo -e "${GREEN}✓ All Kafka integration tests passed!${NC}"
    echo ""
    echo "The tests validated:"
    echo "  • Kafka backend can send messages successfully"
    echo "  • Connection testing works correctly"
    echo "  • End-to-end webhook submission sends to Kafka"
    echo "  • Message metadata (partition, offset) is captured"
    echo ""
    echo "Check your Kafka UI or console consumer to verify messages arrived in:"
    echo "  Topic: webhooks-integration-test"
else
    echo -e "${RED}✗ Some tests failed${NC}"
    echo ""
    echo "Troubleshooting tips:"
    echo "  1. Verify Kafka is running: docker ps | grep kafka"
    echo "  2. Check Kafka logs: docker logs <kafka-container-id>"
    echo "  3. Try connecting manually: docker exec -it <kafka-container-id> kafka-console-producer --topic test --bootstrap-server localhost:9092"
    echo "  4. Review test output above for specific error messages"
fi

exit $TEST_EXIT_CODE
