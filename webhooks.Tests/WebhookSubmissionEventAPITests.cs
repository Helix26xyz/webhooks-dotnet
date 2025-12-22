using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
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
    public class WebhookSubmissionEventAPITests
    {
        private readonly AppDbContext _context;
        private readonly WebhookEventsSubmissionController _controller;
        private readonly Mock<IWebhookBackendFactory> _mockBackendFactory;
        private readonly Mock<ILogger<WebhookEventsSubmissionController>> _mockLogger;
        private readonly Mock<IEncryptionService> _mockEncryptionService;

        public WebhookSubmissionEventAPITests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDatabase_{Guid.NewGuid()}")
                .Options;

            _context = new AppDbContext(options);
            
            // Setup mock backend factory
            _mockBackendFactory = new Mock<IWebhookBackendFactory>();
            _mockLogger = new Mock<ILogger<WebhookEventsSubmissionController>>();
            _mockEncryptionService = new Mock<IEncryptionService>();
            
            // Mock encryption to pass-through for tests
            _mockEncryptionService.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(s => s);
            _mockEncryptionService.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(s => s);
            _mockEncryptionService.Setup(e => e.IsEncrypted(It.IsAny<string>())).Returns(false);
            
            // Setup mock backend to return success
            var mockBackend = new Mock<IWebhookBackend>();
            mockBackend.Setup(b => b.BackendType).Returns(WebhookBackendType.Database);
            mockBackend.Setup(b => b.SendAsync(It.IsAny<Webhook>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WebhookBackendResult
                {
                    Success = true,
                    Message = "Test backend success"
                });
            
            _mockBackendFactory.Setup(f => f.GetBackend(It.IsAny<WebhookBackendType>()))
                .Returns(mockBackend.Object);
            
            _controller = new WebhookEventsSubmissionController(_context, _mockBackendFactory.Object, _mockLogger.Object, _mockEncryptionService.Object);

            // Seed the database with test data
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
            _context.SaveChanges();
        }

        [Fact]
        public async Task GetWebhookEvent_CreatesAndReturnsWebhookEvent()
        {
            // Act
            var result = await _controller.GetWebhookEvent("test-org", "test-project", "test-webhook");

            // Assert
            var actionResult = Assert.IsType<ActionResult<WebhookEvent>>(result);
            var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var webhookEvent = Assert.IsType<WebhookEvent>(createdResult.Value);
            Assert.Equal(WebhookEventStatus.New, webhookEvent.Status);
            Assert.Equal(WebhookEventSubStatus.Pending, webhookEvent.SubStatus);
        }

        [Fact]
        public async Task GetWebhookEvent_ReturnsNotFound_WhenWebhookDoesNotExist()
        {
            // Act
            var result = await _controller.GetWebhookEvent("invalid-org", "invalid-project", "invalid-webhook");

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task PostWebhookEvent_CreatesAndReturnsWebhookEvent()
        {
            // Arrange
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
        }

        [Fact]
        public async Task PostWebhookEvent_ReturnsNotFound_WhenWebhookDoesNotExist()
        {
            // Arrange
            var payload = new { message = "Test payload" };

            // Act
            var result = await _controller.PostWebhookEvent("invalid-org", "invalid-project", "invalid-webhook", payload);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }
    }
}
