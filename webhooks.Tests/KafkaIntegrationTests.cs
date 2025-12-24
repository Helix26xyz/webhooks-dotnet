using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading.Tasks;
using webhooks.ApiService.src;
using webhooks.SharedModels.backends;
using webhooks.SharedModels.models;
using webhooks.SharedModels.security;
using webhooks.SharedModels.storage;
using Xunit;

namespace webhooks.ApiService.Tests
{
    /// <summary>
    /// Integration tests for Kafka backend that validate end-to-end webhook processing
    /// These tests require a running Kafka instance at localhost:9092
    /// </summary>
    public class KafkaIntegrationTests
    {
        private const string KafkaBootstrapServers = "localhost:9092";
        private const string TestTopic = "webhooks-integration-test";
        
        [Fact]
        public async Task DirectKafkaProducer_SendToRemoteServer_Success()
        {
            // Arrange - Direct Kafka producer test to remote VM
            var config = new Confluent.Kafka.ProducerConfig
            {
                BootstrapServers = "10.10.100.93:9092",
                ClientId = "direct-test-producer",
                Acks = Confluent.Kafka.Acks.Leader,
                MessageTimeoutMs = 30000,
                RequestTimeoutMs = 15000,
                SocketTimeoutMs = 15000,
                MessageSendMaxRetries = 3,
                RetryBackoffMs = 500
            };

            var testPayload = new
            {
                test = "direct-kafka-test",
                timestamp = DateTime.UtcNow.ToString("o"),
                message = "Testing direct Kafka connectivity from .NET"
            };

            var payloadJson = System.Text.Json.JsonSerializer.Serialize(testPayload);

            // Act
            using var producer = new Confluent.Kafka.ProducerBuilder<string, string>(config).Build();
            
            var message = new Confluent.Kafka.Message<string, string>
            {
                Key = Guid.NewGuid().ToString(),
                Value = payloadJson
            };

            var deliveryResult = await producer.ProduceAsync("test-topic", message);

            // Assert
            Assert.NotNull(deliveryResult);
            Assert.Equal(Confluent.Kafka.PersistenceStatus.Persisted, deliveryResult.Status);
            
            // Log results
            Console.WriteLine($"✅ Message sent successfully!");
            Console.WriteLine($"   Topic: {deliveryResult.Topic}");
            Console.WriteLine($"   Partition: {deliveryResult.Partition.Value}");
            Console.WriteLine($"   Offset: {deliveryResult.Offset.Value}");
            Console.WriteLine($"   Timestamp: {deliveryResult.Timestamp.UtcDateTime}");
        }

        [Fact(Skip = "Integration test - requires Kafka running at localhost:9092")]
        public async Task KafkaBackend_SendAsync_ActuallySendsToKafka()
        {
            // Arrange
            var logger = new Mock<ILogger<KafkaBackend>>();
            var kafkaBackend = new KafkaBackend(logger.Object);

            var config = $"{{\"BootstrapServers\":\"{KafkaBootstrapServers}\",\"Topic\":\"{TestTopic}\"}}";
            var webhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "Integration Test Webhook",
                BackendType = WebhookBackendType.Kafka,
                BackendConfig = config // Plain JSON, not encrypted for this test
            };

            var payload = "{\"test\":\"integration\",\"timestamp\":\"" + DateTime.UtcNow.ToString("o") + "\"}";
            var eventId = Guid.NewGuid();

            // Act
            var result = await kafkaBackend.SendAsync(webhook, payload, eventId);

            // Assert
            Assert.True(result.Success, $"Kafka send should succeed. Error: {result.Message}");
            Assert.NotNull(result.Message);
            Assert.Contains("partition", result.Message.ToLower());
            Assert.Contains("offset", result.Message.ToLower());
            Assert.NotNull(result.BackendReference);
            Assert.Contains(KafkaBootstrapServers, result.BackendReference);
            Assert.Contains(TestTopic, result.BackendReference);
            Assert.Contains("partition:", result.BackendReference);
            Assert.Contains("offset:", result.BackendReference);
        }

        [Fact(Skip = "Integration test - requires Kafka running at localhost:9092")]
        public async Task KafkaBackend_TestConnection_ValidatesKafkaConnectivity()
        {
            // Arrange
            var logger = new Mock<ILogger<KafkaBackend>>();
            var kafkaBackend = new KafkaBackend(logger.Object);

            var config = $"{{\"BootstrapServers\":\"{KafkaBootstrapServers}\",\"Topic\":\"{TestTopic}\"}}";
            var webhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "Connection Test Webhook",
                BackendType = WebhookBackendType.Kafka,
                BackendConfig = config
            };

            // Act
            var result = await kafkaBackend.TestConnectionAsync(webhook);

            // Assert
            Assert.True(result, "Kafka connection test should succeed when Kafka is available");
        }

        [Fact(Skip = "Integration test - requires Kafka running at localhost:9092")]
        public async Task EndToEnd_WebhookWithKafkaBackend_SendsEventToKafka()
        {
            // Arrange - Setup database and services
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"KafkaIntegrationTest_{Guid.NewGuid()}")
                .Options;
            var context = new AppDbContext(options);

            var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            mockConfig.Setup(c => c["Encryption:SecretKey"]).Returns("ThisIsAVerySecureSecretKey123456789012");
            var encryptionLogger = new Mock<ILogger<AesEncryptionService>>();
            var encryptionService = new AesEncryptionService(mockConfig.Object, encryptionLogger.Object);

            // Setup service provider for factory
            var serviceCollection = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            serviceCollection.AddLogging();
            serviceCollection.AddSingleton<IWebhookBackend, DatabaseBackend>();
            serviceCollection.AddSingleton<IWebhookBackend, KafkaBackend>();
            serviceCollection.AddSingleton<IWebhookBackend, RabbitMQBackend>();
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var factoryLogger = new Mock<ILogger<WebhookBackendFactory>>();
            var backendFactory = new WebhookBackendFactory(serviceProvider, factoryLogger.Object);

            var submissionLogger = new Mock<ILogger<WebhookEventsSubmissionController>>();
            var submissionController = new WebhookEventsSubmissionController(
                context, 
                backendFactory, 
                submissionLogger.Object, 
                encryptionService
            );

            // Create a webhook with Kafka backend
            var kafkaConfig = $"{{\"BootstrapServers\":\"{KafkaBootstrapServers}\",\"Topic\":\"{TestTopic}\"}}";
            var webhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "End-to-End Test Webhook",
                Slug = "e2e-test-webhook",
                Owner = "integration-test",
                Project = "test-project",
                Status = WebhookStatus.Enabled,
                BackendType = WebhookBackendType.Kafka,
                DeliveryMode = WebhookDeliveryMode.Synchronous
            };
            webhook.SetBackendConfig(kafkaConfig, encryptionService);

            context.Webhooks.Add(webhook);
            await context.SaveChangesAsync();

            // Prepare test payload
            var payload = new
            {
                eventType = "integration.test",
                timestamp = DateTime.UtcNow.ToString("o"),
                testId = Guid.NewGuid().ToString(),
                data = new
                {
                    message = "End-to-end integration test",
                    value = 99999
                }
            };

            // Act - Submit webhook event
            var result = await submissionController.PostWebhookEvent(
                webhook.Owner,
                webhook.Project,
                webhook.Slug,
                payload
            );

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var webhookEvent = Assert.IsType<WebhookEvent>(createdResult.Value);

            Assert.NotNull(webhookEvent);
            Assert.Equal(WebhookEventStatus.Processed, webhookEvent.Status);
            Assert.Equal(WebhookEventSubStatus.Success, webhookEvent.SubStatus);
            Assert.NotNull(webhookEvent.StatusResultText);
            Assert.Contains("partition", webhookEvent.StatusResultText.ToLower());
            Assert.Contains("offset", webhookEvent.StatusResultText.ToLower());

            // Verify the event was stored in DB
            var storedEvent = await context.WebhookEvents.FindAsync(webhookEvent.Id);
            Assert.NotNull(storedEvent);
            Assert.Equal(WebhookEventStatus.Processed, storedEvent.Status);
            Assert.Equal(WebhookEventSubStatus.Success, storedEvent.SubStatus);
        }

        [Fact]
        public async Task KafkaBackend_WithInvalidBootstrapServers_ReturnsError()
        {
            // Arrange
            var logger = new Mock<ILogger<KafkaBackend>>();
            var kafkaBackend = new KafkaBackend(logger.Object);

            var config = "{\"BootstrapServers\":\"invalid-server:9092\",\"Topic\":\"test-topic\"}";
            var webhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "Invalid Server Test",
                BackendType = WebhookBackendType.Kafka,
                BackendConfig = config
            };

            var payload = "{\"test\":\"data\"}";
            var eventId = Guid.NewGuid();

            // Act
            var result = await kafkaBackend.SendAsync(webhook, payload, eventId);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Kafka error", result.Message);
        }

        [Fact]
        public async Task KafkaBackend_WithMissingBootstrapServers_ReturnsError()
        {
            // Arrange
            var logger = new Mock<ILogger<KafkaBackend>>();
            var kafkaBackend = new KafkaBackend(logger.Object);

            var config = "{\"Topic\":\"test-topic\"}"; // Missing BootstrapServers
            var webhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "Missing Config Test",
                BackendType = WebhookBackendType.Kafka,
                BackendConfig = config
            };

            var payload = "{\"test\":\"data\"}";
            var eventId = Guid.NewGuid();

            // Act
            var result = await kafkaBackend.SendAsync(webhook, payload, eventId);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not configured", result.Message);
        }

        [Fact]
        public async Task KafkaBackend_WithMissingTopic_ReturnsError()
        {
            // Arrange
            var logger = new Mock<ILogger<KafkaBackend>>();
            var kafkaBackend = new KafkaBackend(logger.Object);

            var config = "{\"BootstrapServers\":\"localhost:9092\"}"; // Missing Topic
            var webhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "Missing Topic Test",
                BackendType = WebhookBackendType.Kafka,
                BackendConfig = config
            };

            var payload = "{\"test\":\"data\"}";
            var eventId = Guid.NewGuid();

            // Act
            var result = await kafkaBackend.SendAsync(webhook, payload, eventId);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not configured", result.Message);
        }
    }
}
