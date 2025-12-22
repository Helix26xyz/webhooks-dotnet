using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
    public class WebhookE2ETests
    {
        private readonly AppDbContext _context;
        private readonly WebhooksController _webhookcontroller;
        private readonly WebhookEventsController _eventcontroller;
        private readonly WebhookEventsSubmissionController _submissioncontroller;
        private Webhook _webhook;
        private WebhookEvent _webhookEvent;
        private readonly Mock<IWebhookBackendFactory> _mockBackendFactory;
        private readonly Mock<IEncryptionService> _mockEncryptionService;

        private Guid _webhookId;
        
        public WebhookE2ETests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDatabase_{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context = new AppDbContext(options);
            
            // Setup mock encryption service
            _mockEncryptionService = new Mock<IEncryptionService>();
            _mockEncryptionService.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(s => s);
            _mockEncryptionService.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(s => s);
            _mockEncryptionService.Setup(e => e.IsEncrypted(It.IsAny<string>())).Returns(false);
            
            // Setup mock backend factory
            _mockBackendFactory = new Mock<IWebhookBackendFactory>();
            var mockLogger = new Mock<ILogger<WebhookEventsSubmissionController>>();
            
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
            
            // Initialize controllers
            _webhookcontroller = new WebhooksController(_context, _mockEncryptionService.Object, _mockBackendFactory.Object);
            _eventcontroller = new WebhookEventsController(_context);
            _submissioncontroller = new WebhookEventsSubmissionController(_context, _mockBackendFactory.Object, mockLogger.Object, _mockEncryptionService.Object);

            _webhookId = Guid.NewGuid();
            // Seed the database with test data
            _webhook = new Webhook
            {
                Id = _webhookId,
                Name = $"TestWebhook{_webhookId}",
                Slug = $"test-webhook{_webhookId}",
                Owner = $"test-org{_webhookId}",
                Project = $"test-project{_webhookId}",
                Status = WebhookStatus.Enabled,
                BackendType = WebhookBackendType.Database,
                DeliveryMode = WebhookDeliveryMode.Synchronous
            };
            _context.Webhooks.Add(_webhook);
            _context.SaveChanges();
        }

        [Fact]
        public async Task PostWebhookEvent_CreatesAndReturnsWebhookEvent()
        {
            // Arrange
            var payload = new { message = "Test payload" };

            // Act
            var result = await _submissioncontroller.PostWebhookEvent(_webhook.Owner, _webhook.Project, _webhook.Slug, payload);
            var objectResult = result.Result as ObjectResult;
            _webhookEvent = objectResult?.Value as WebhookEvent;
            
            // Assert
            var webhookEvent = Assert.IsType<WebhookEvent>(_webhookEvent);
            Assert.Equal(WebhookEventStatus.Processed, webhookEvent.Status);
            Assert.Equal(WebhookEventSubStatus.Success, webhookEvent.SubStatus);
            Assert.Contains("Test payload", webhookEvent.Payload);

            // Since backend processed synchronously, the event is already Processed
            // ReceiveWebhookEvent should return NoContent since there are no New events
            var result2 = await _eventcontroller.ReceiveWebhookEvent(_webhook.Id);
            Assert.IsType<NoContentResult>(result2.Result);
        }

    }
}
