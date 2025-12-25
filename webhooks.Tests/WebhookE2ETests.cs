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
            
            // Setup HttpContext for submission controller (needed for Request.Query)
            _submissioncontroller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            };

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
        public async Task DatabaseBackend_FullWorkflow_SubmitReceiveAndProcess()
        {
            // Arrange
            var payload = new { message = "Test payload" };

            // Act 1: Submit webhook event
            var submitResult = await _submissioncontroller.PostWebhookEvent(_webhook.Owner, _webhook.Project, _webhook.Slug, payload);
            
            // Assert 1: Event should be created with New status (awaiting consumer retrieval)
            var actionResult = Assert.IsType<ActionResult<WebhookEventDto>>(submitResult);
            var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var webhookEventDto = Assert.IsType<WebhookEventDto>(createdResult.Value);
            Assert.Equal(WebhookEventStatus.New, webhookEventDto.Status);
            Assert.Equal(WebhookEventSubStatus.Pending, webhookEventDto.SubStatus);

            // Act 2: Consumer retrieves the event via /receive endpoint
            var receiveResult = await _eventcontroller.ReceiveWebhookEvent(_webhook.Id);
            
            // Assert 2: Event should be returned and marked as Received
            var receiveOkResult = Assert.IsType<OkObjectResult>(receiveResult.Result);
            var receivedEvent = Assert.IsType<WebhookEvent>(receiveOkResult.Value);
            Assert.Equal(WebhookEventStatus.Received, receivedEvent.Status);
            Assert.Contains("Test payload", receivedEvent.Payload);

            // Act 3: Consumer processes and returns result
            var returnResult = await _eventcontroller.ReturnWebhookEvent(receivedEvent.Id, new WebhookEventWorkResponse
            {
                Status = WebhookEventSubStatus.Success,
                ResultText = "Processed successfully"
            });
            
            // Assert 3: Event should be marked as Processed
            var returnOkResult = Assert.IsType<OkObjectResult>(returnResult.Result);
            var processedEvent = Assert.IsType<WebhookEvent>(returnOkResult.Value);
            Assert.Equal(WebhookEventStatus.Processed, processedEvent.Status);
            Assert.Equal(WebhookEventSubStatus.Success, processedEvent.SubStatus);

            // Act 4: Try to receive again - should get NoContent since no more New events
            var receiveResult2 = await _eventcontroller.ReceiveWebhookEvent(_webhook.Id);
            Assert.IsType<NoContentResult>(receiveResult2.Result);
        }

        [Fact]
        public async Task KafkaBackend_MarksEventAsProcessedImmediately()
        {
            // Arrange - Create a Kafka webhook
            var kafkaWebhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "KafkaTestWebhook",
                Slug = "kafka-test-webhook",
                Owner = "kafka-org",
                Project = "kafka-project",
                Status = WebhookStatus.Enabled,
                BackendType = WebhookBackendType.Kafka,
                DeliveryMode = WebhookDeliveryMode.Synchronous,
                BackendConfig = "{\"BootstrapServers\":\"localhost:9092\",\"Topic\":\"test-topic\"}"
            };
            _context.Webhooks.Add(kafkaWebhook);
            _context.SaveChanges();

            // Setup Kafka backend mock
            var mockKafkaBackend = new Mock<IWebhookBackend>();
            mockKafkaBackend.Setup(b => b.BackendType).Returns(WebhookBackendType.Kafka);
            mockKafkaBackend.Setup(b => b.SendAsync(It.IsAny<Webhook>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WebhookBackendResult
                {
                    Success = true,
                    Message = "Sent to Kafka"
                });
            _mockBackendFactory.Setup(f => f.GetBackend(WebhookBackendType.Kafka))
                .Returns(mockKafkaBackend.Object);

            var payload = new { message = "Kafka test payload" };

            // Act: Submit webhook event
            var submitResult = await _submissioncontroller.PostWebhookEvent(kafkaWebhook.Owner, kafkaWebhook.Project, kafkaWebhook.Slug, payload);
            
            // Assert 1: Event should be immediately marked as Processed (not awaiting consumer retrieval)
            var actionResult = Assert.IsType<ActionResult<WebhookEventDto>>(submitResult);
            var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var webhookEventDto = Assert.IsType<WebhookEventDto>(createdResult.Value);
            Assert.Equal(WebhookEventStatus.Processed, webhookEventDto.Status);
            Assert.Equal(WebhookEventSubStatus.Success, webhookEventDto.SubStatus);

            // Assert 2: Receive endpoint should return NoContent (no events awaiting retrieval)
            var receiveResult = await _eventcontroller.ReceiveWebhookEvent(kafkaWebhook.Id);
            Assert.IsType<NoContentResult>(receiveResult.Result);
        }

    }
}
