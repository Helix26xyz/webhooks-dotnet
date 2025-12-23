using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using webhooks.ApiService.src;
using webhooks.SharedModels.models;
using webhooks.SharedModels.storage;
using webhooks.SharedModels.backends;
using webhooks.SharedModels.security;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace webhooks.ApiService.Tests
{
    public class WebhookBackendTests
    {
        private readonly AppDbContext _context;
        private readonly Mock<IWebhookBackendFactory> _mockBackendFactory;
        private readonly Mock<ILogger<WebhookEventsSubmissionController>> _mockLogger;
        private readonly Mock<IEncryptionService> _mockEncryptionService;
        private readonly WebhookEventsSubmissionController _controller;

        public WebhookBackendTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDatabase_{Guid.NewGuid()}")
                .Options;

            _context = new AppDbContext(options);
            _mockBackendFactory = new Mock<IWebhookBackendFactory>();
            _mockLogger = new Mock<ILogger<WebhookEventsSubmissionController>>();
            _mockEncryptionService = new Mock<IEncryptionService>();
            
            // Mock encryption to pass-through for tests
            _mockEncryptionService.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(s => s);
            _mockEncryptionService.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(s => s);
            _mockEncryptionService.Setup(e => e.IsEncrypted(It.IsAny<string>())).Returns(false);
            
            _controller = new WebhookEventsSubmissionController(_context, _mockBackendFactory.Object, _mockLogger.Object, _mockEncryptionService.Object);
        }

        [Fact]
        public async Task PostWebhookEvent_WithDatabaseBackend_SavesAndProcesses()
        {
            // Arrange
            var mockBackend = new Mock<IWebhookBackend>();
            mockBackend.Setup(b => b.BackendType).Returns(WebhookBackendType.Database);
            mockBackend.Setup(b => b.SendAsync(It.IsAny<Webhook>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WebhookBackendResult
                {
                    Success = true,
                    Message = "Database backend success",
                    BackendReference = Guid.NewGuid().ToString()
                });

            _mockBackendFactory.Setup(f => f.GetBackend(WebhookBackendType.Database))
                .Returns(mockBackend.Object);

            var webhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "TestWebhook",
                Slug = "test-webhook",
                Owner = "test-org",
                Project = "test-project",
                Status = WebhookStatus.Enabled,
                BackendType = WebhookBackendType.Database,
                DeliveryMode = WebhookDeliveryMode.Synchronous
            };
            _context.Webhooks.Add(webhook);
            await _context.SaveChangesAsync();

            var payload = new { message = "Test payload" };

            // Act
            var result = await _controller.PostWebhookEvent("test-org", "test-project", "test-webhook", payload);

            // Assert
            var actionResult = Assert.IsType<ActionResult<WebhookEvent>>(result);
            var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var webhookEvent = Assert.IsType<WebhookEvent>(createdResult.Value);
            
            Assert.Equal(WebhookEventStatus.Processed, webhookEvent.Status);
            Assert.Equal(WebhookEventSubStatus.Success, webhookEvent.SubStatus);
            Assert.Contains("Test payload", webhookEvent.Payload);
            
            // Verify backend was called
            mockBackend.Verify(b => b.SendAsync(
                It.IsAny<Webhook>(), 
                It.IsAny<string>(), 
                It.IsAny<Guid>(), 
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task PostWebhookEvent_WithKafkaBackend_SavesAndProcesses()
        {
            // Arrange
            var mockBackend = new Mock<IWebhookBackend>();
            mockBackend.Setup(b => b.BackendType).Returns(WebhookBackendType.Kafka);
            mockBackend.Setup(b => b.SendAsync(It.IsAny<Webhook>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WebhookBackendResult
                {
                    Success = true,
                    Message = "Kafka backend success",
                    BackendReference = "kafka://localhost:9092/webhooks"
                });

            _mockBackendFactory.Setup(f => f.GetBackend(WebhookBackendType.Kafka))
                .Returns(mockBackend.Object);

            var webhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "TestWebhook",
                Slug = "test-kafka-webhook",
                Owner = "test-org",
                Project = "test-project",
                Status = WebhookStatus.Enabled,
                BackendType = WebhookBackendType.Kafka,
                BackendConfig = "{\"BootstrapServers\":\"localhost:9092\",\"Topic\":\"webhooks\"}",
                DeliveryMode = WebhookDeliveryMode.Synchronous
            };
            _context.Webhooks.Add(webhook);
            await _context.SaveChangesAsync();

            var payload = new { message = "Kafka payload" };

            // Act
            var result = await _controller.PostWebhookEvent("test-org", "test-project", "test-kafka-webhook", payload);

            // Assert
            var actionResult = Assert.IsType<ActionResult<WebhookEvent>>(result);
            var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var webhookEvent = Assert.IsType<WebhookEvent>(createdResult.Value);
            
            Assert.Equal(WebhookEventStatus.Processed, webhookEvent.Status);
            Assert.Equal(WebhookEventSubStatus.Success, webhookEvent.SubStatus);
            
            // Verify backend was called
            mockBackend.Verify(b => b.SendAsync(
                It.IsAny<Webhook>(), 
                It.IsAny<string>(), 
                It.IsAny<Guid>(), 
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task PostWebhookEvent_WithBackendFailure_RecordsFailure()
        {
            // Arrange
            var mockBackend = new Mock<IWebhookBackend>();
            mockBackend.Setup(b => b.BackendType).Returns(WebhookBackendType.Kafka);
            mockBackend.Setup(b => b.SendAsync(It.IsAny<Webhook>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WebhookBackendResult
                {
                    Success = false,
                    Message = "Backend connection failed"
                });

            _mockBackendFactory.Setup(f => f.GetBackend(WebhookBackendType.Kafka))
                .Returns(mockBackend.Object);

            var webhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "TestWebhook",
                Slug = "test-failing-webhook",
                Owner = "test-org",
                Project = "test-project",
                Status = WebhookStatus.Enabled,
                BackendType = WebhookBackendType.Kafka,
                DeliveryMode = WebhookDeliveryMode.Synchronous
            };
            _context.Webhooks.Add(webhook);
            await _context.SaveChangesAsync();

            var payload = new { message = "Failing payload" };

            // Act
            var result = await _controller.PostWebhookEvent("test-org", "test-project", "test-failing-webhook", payload);

            // Assert
            var actionResult = Assert.IsType<ActionResult<WebhookEvent>>(result);
            var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var webhookEvent = Assert.IsType<WebhookEvent>(createdResult.Value);
            
            Assert.Equal(WebhookEventStatus.Processed, webhookEvent.Status);
            Assert.Equal(WebhookEventSubStatus.Failed, webhookEvent.SubStatus);
            Assert.Contains("Backend connection failed", webhookEvent.StatusResultText);
        }

        [Fact]
        public async Task PostWebhookEvent_UpdatesLastReceivedAt()
        {
            // Arrange
            var mockBackend = new Mock<IWebhookBackend>();
            mockBackend.Setup(b => b.BackendType).Returns(WebhookBackendType.Database);
            mockBackend.Setup(b => b.SendAsync(It.IsAny<Webhook>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WebhookBackendResult { Success = true, Message = "OK" });

            _mockBackendFactory.Setup(f => f.GetBackend(It.IsAny<WebhookBackendType>()))
                .Returns(mockBackend.Object);

            var webhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "TestWebhook",
                Slug = "test-timestamp-webhook",
                Owner = "test-org",
                Project = "test-project",
                Status = WebhookStatus.Enabled,
                BackendType = WebhookBackendType.Database,
                DeliveryMode = WebhookDeliveryMode.Synchronous,
                LastReceivedAt = null
            };
            _context.Webhooks.Add(webhook);
            await _context.SaveChangesAsync();

            var payload = new { message = "Timestamp test" };

            // Act
            await _controller.PostWebhookEvent("test-org", "test-project", "test-timestamp-webhook", payload);

            // Assert
            var updatedWebhook = await _context.Webhooks.FindAsync(webhook.Id);
            Assert.NotNull(updatedWebhook);
            Assert.NotNull(updatedWebhook.LastReceivedAt);
            Assert.True(updatedWebhook.LastReceivedAt > DateTime.UtcNow.AddSeconds(-5));
        }

        [Fact]
        public void EncryptionService_EncryptsAndDecrypts_Successfully()
        {
            // Arrange
            var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            mockConfig.Setup(c => c["Encryption:SecretKey"]).Returns("ThisIsAVerySecureSecretKey123456789012");
            var mockLogger = new Mock<ILogger<AesEncryptionService>>();
            var encryptionService = new AesEncryptionService(mockConfig.Object, mockLogger.Object);

            var plaintext = "{\"BootstrapServers\":\"localhost:9092\",\"Topic\":\"webhooks\"}";

            // Act
            var encrypted = encryptionService.Encrypt(plaintext);
            var decrypted = encryptionService.Decrypt(encrypted);

            // Assert
            Assert.NotEqual(plaintext, encrypted);
            Assert.True(encryptionService.IsEncrypted(encrypted));
            Assert.StartsWith("ENC:", encrypted);
            Assert.Equal(plaintext, decrypted);
        }

        [Fact]
        public void EncryptionService_DoesNotDoubleEncrypt()
        {
            // Arrange
            var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            mockConfig.Setup(c => c["Encryption:SecretKey"]).Returns("ThisIsAVerySecureSecretKey123456789012");
            var mockLogger = new Mock<ILogger<AesEncryptionService>>();
            var encryptionService = new AesEncryptionService(mockConfig.Object, mockLogger.Object);

            var plaintext = "{\"BootstrapServers\":\"localhost:9092\",\"Topic\":\"webhooks\"}";

            // Act
            var encrypted1 = encryptionService.Encrypt(plaintext);
            var encrypted2 = encryptionService.Encrypt(encrypted1); // Try to encrypt again

            // Assert
            Assert.Equal(encrypted1, encrypted2); // Should not double-encrypt
        }

        [Fact]
        public void EncryptionService_Decrypt_ReturnsPlaintextIfNotEncrypted()
        {
            // Arrange
            var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            mockConfig.Setup(c => c["Encryption:SecretKey"]).Returns("ThisIsAVerySecureSecretKey123456789012");
            var mockLogger = new Mock<ILogger<AesEncryptionService>>();
            var encryptionService = new AesEncryptionService(mockConfig.Object, mockLogger.Object);

            var plaintext = "{\"BootstrapServers\":\"localhost:9092\",\"Topic\":\"webhooks\"}";

            // Act
            var result = encryptionService.Decrypt(plaintext);

            // Assert
            Assert.Equal(plaintext, result); // Should return as-is for backward compatibility
        }

        [Fact]
        public async Task TestConnection_WithEncryptedConfig_DecryptsForBackend()
        {
            // Arrange
            var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            mockConfig.Setup(c => c["Encryption:SecretKey"]).Returns("ThisIsAVerySecureSecretKey123456789012");
            var encryptionLogger = new Mock<ILogger<AesEncryptionService>>();
            var encryptionService = new AesEncryptionService(mockConfig.Object, encryptionLogger.Object);

            var kafkaLogger = new Mock<ILogger<KafkaBackend>>();
            var kafkaBackend = new KafkaBackend(kafkaLogger.Object);

            var mockBackendFactory = new Mock<IWebhookBackendFactory>();
            mockBackendFactory.Setup(f => f.GetBackend(WebhookBackendType.Kafka)).Returns(kafkaBackend);

            var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDatabase_{Guid.NewGuid()}")
                .Options);

            var controller = new WebhooksController(context, encryptionService, mockBackendFactory.Object);

            var plainConfig = "{\"BootstrapServers\":\"localhost:9092\",\"Topic\":\"test-topic\"}";
            var webhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "TestKafkaWebhook",
                Slug = "test-kafka",
                BackendType = WebhookBackendType.Kafka,
                BackendConfig = plainConfig // Sent as plain JSON from UI
            };

            // Act
            var result = await controller.TestConnection(webhook);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var backendResult = Assert.IsType<WebhookBackendResult>(okResult.Value);
            Assert.True(backendResult.Success, $"Connection test should succeed, but got: {backendResult.Message}");
        }

        [Fact]
        public async Task KafkaBackend_ParsesDecryptedConfig_Successfully()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<KafkaBackend>>();
            var kafkaBackend = new KafkaBackend(mockLogger.Object);

            var plainConfig = "{\"BootstrapServers\":\"localhost:9092\",\"Topic\":\"test-topic\"}";
            var webhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "TestKafkaWebhook",
                BackendType = WebhookBackendType.Kafka,
                BackendConfig = plainConfig // DECRYPTED config
            };

            // Act
            var result = await kafkaBackend.TestConnectionAsync(webhook);

            // Assert - should not throw JSON exception
            Assert.True(result); // Test passes if no exception thrown
        }

        [Fact]
        public async Task KafkaBackend_WithEncryptedConfig_FailsToParseAndReturnsDefaultConfig()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<KafkaBackend>>();
            var kafkaBackend = new KafkaBackend(mockLogger.Object);

            // Simulate what would happen if encrypted config reached the backend
            var encryptedConfig = "ENC:SGVsbG9Xb3JsZA=="; // This is NOT valid JSON
            var webhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "TestKafkaWebhook",
                BackendType = WebhookBackendType.Kafka,
                BackendConfig = encryptedConfig // ENCRYPTED config (wrong!)
            };

            // Act
            var result = await kafkaBackend.TestConnectionAsync(webhook);

            // Assert - should fail because config can't be parsed
            Assert.False(result); // Should return false due to invalid config
        }

        [Fact]
        public async Task RabbitMQBackend_ParsesDecryptedConfig_Successfully()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<RabbitMQBackend>>();
            var rabbitBackend = new RabbitMQBackend(mockLogger.Object);

            var plainConfig = "{\"HostName\":\"localhost\",\"QueueName\":\"test-queue\",\"UserName\":\"guest\",\"Password\":\"guest\"}";
            var webhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "TestRabbitWebhook",
                BackendType = WebhookBackendType.RabbitMQ,
                BackendConfig = plainConfig // DECRYPTED config
            };

            // Act
            var result = await rabbitBackend.TestConnectionAsync(webhook);

            // Assert - should not throw JSON exception
            Assert.True(result); // Test passes if no exception thrown
        }

        [Fact]
        public void GetWebhookForBackend_DecryptsBackendConfig()
        {
            // Arrange
            var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            mockConfig.Setup(c => c["Encryption:SecretKey"]).Returns("ThisIsAVerySecureSecretKey123456789012");
            var mockLogger = new Mock<ILogger<AesEncryptionService>>();
            var encryptionService = new AesEncryptionService(mockConfig.Object, mockLogger.Object);

            var plainConfig = "{\"BootstrapServers\":\"localhost:9092\",\"Topic\":\"webhooks\"}";
            var encryptedConfig = encryptionService.Encrypt(plainConfig);

            var webhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "TestWebhook",
                BackendType = WebhookBackendType.Kafka,
                BackendConfig = encryptedConfig // Stored as encrypted
            };

            // Act
            var webhookForBackend = webhook.GetWebhookForBackend(encryptionService);

            // Assert
            Assert.NotEqual(webhook.BackendConfig, webhookForBackend.BackendConfig);
            Assert.Equal(plainConfig, webhookForBackend.BackendConfig);
            Assert.False(encryptionService.IsEncrypted(webhookForBackend.BackendConfig));
        }
    }
}
