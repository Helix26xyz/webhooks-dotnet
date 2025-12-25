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

        // ===================================================================
        // PRIORITY 1 TESTS: Asynchronous Delivery Mode
        // ===================================================================

        [Fact]
        public async Task AsyncDelivery_Database_ReturnsImmediatelyAndKeepsStatusNew()
        {
            // Arrange - Create webhook with ASYNC delivery mode
            var asyncWebhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "AsyncDatabaseWebhook",
                Slug = "async-database-webhook",
                Owner = "async-org",
                Project = "async-project",
                Status = WebhookStatus.Enabled,
                BackendType = WebhookBackendType.Database,
                DeliveryMode = WebhookDeliveryMode.Asynchronous // KEY: Async mode
            };
            _context.Webhooks.Add(asyncWebhook);
            _context.SaveChanges();

            var payload = new { message = "Async test payload" };

            // Act - Submit webhook event with async delivery
            var submitResult = await _submissioncontroller.PostWebhookEvent(asyncWebhook.Owner, asyncWebhook.Project, asyncWebhook.Slug, payload);
            
            // Assert 1: Response should be immediate (Created status)
            var actionResult = Assert.IsType<ActionResult<WebhookEventDto>>(submitResult);
            var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var webhookEventDto = Assert.IsType<WebhookEventDto>(createdResult.Value);
            
            // Assert 2: Database backend with async delivery should still keep status as New (awaiting consumer)
            Assert.Equal(WebhookEventStatus.New, webhookEventDto.Status);
            Assert.Equal(WebhookEventSubStatus.Pending, webhookEventDto.SubStatus);
            
            // Assert 3: Event should be retrievable by consumer (since Database backend keeps it New)
            var receiveResult = await _eventcontroller.ReceiveWebhookEvent(asyncWebhook.Id);
            var receiveOkResult = Assert.IsType<OkObjectResult>(receiveResult.Result);
            var receivedEvent = Assert.IsType<WebhookEvent>(receiveOkResult.Value);
            Assert.Equal(WebhookEventStatus.Received, receivedEvent.Status);
        }

        [Fact]
        public async Task AsyncDelivery_Kafka_ReturnsImmediatelyAndEventuallyMarksProcessed()
        {
            // Arrange - Create Kafka webhook with ASYNC delivery mode
            var asyncKafkaWebhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "AsyncKafkaWebhook",
                Slug = "async-kafka-webhook",
                Owner = "kafka-async-org",
                Project = "kafka-async-project",
                Status = WebhookStatus.Enabled,
                BackendType = WebhookBackendType.Kafka,
                DeliveryMode = WebhookDeliveryMode.Asynchronous, // KEY: Async mode
                BackendConfig = "{\"BootstrapServers\":\"localhost:9092\",\"Topic\":\"async-test-topic\"}"
            };
            _context.Webhooks.Add(asyncKafkaWebhook);
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

            var payload = new { message = "Async Kafka payload" };

            // Act - Submit webhook event with async delivery
            var submitResult = await _submissioncontroller.PostWebhookEvent(asyncKafkaWebhook.Owner, asyncKafkaWebhook.Project, asyncKafkaWebhook.Slug, payload);
            
            // Assert 1: Response should be immediate (Created status) with initial New status
            var actionResult = Assert.IsType<ActionResult<WebhookEventDto>>(submitResult);
            var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var webhookEventDto = Assert.IsType<WebhookEventDto>(createdResult.Value);
            
            // For async mode, event starts as New but background task will update it
            Assert.Equal(WebhookEventStatus.New, webhookEventDto.Status);
            
            // Assert 2: Wait for background task to complete (give it up to 3 seconds)
            var eventId = webhookEventDto.Id;
            WebhookEvent? updatedEvent = null;
            for (int i = 0; i < 30; i++) // Poll for up to 3 seconds
            {
                await Task.Delay(100);
                updatedEvent = await _context.WebhookEvents.FindAsync(eventId);
                if (updatedEvent?.Status == WebhookEventStatus.Processed)
                    break;
            }
            
            // Assert 3: Eventually the event should be marked as Processed by background task
            Assert.NotNull(updatedEvent);
            Assert.Equal(WebhookEventStatus.Processed, updatedEvent.Status);
            Assert.Equal(WebhookEventSubStatus.Success, updatedEvent.SubStatus);
            
            // Assert 4: Kafka backend should NOT be retrievable by consumer (marked as Processed)
            var receiveResult = await _eventcontroller.ReceiveWebhookEvent(asyncKafkaWebhook.Id);
            Assert.IsType<NoContentResult>(receiveResult.Result);
        }

        [Fact]
        public async Task AsyncDelivery_KafkaFailure_EventuallyMarksAsFailed()
        {
            // Arrange - Kafka webhook with async delivery that will fail
            var failingKafkaWebhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "FailingAsyncKafkaWebhook",
                Slug = "failing-async-kafka",
                Owner = "fail-org",
                Project = "fail-project",
                Status = WebhookStatus.Enabled,
                BackendType = WebhookBackendType.Kafka,
                DeliveryMode = WebhookDeliveryMode.Asynchronous,
                BackendConfig = "{\"BootstrapServers\":\"localhost:9092\",\"Topic\":\"fail-topic\"}"
            };
            _context.Webhooks.Add(failingKafkaWebhook);
            _context.SaveChanges();

            // Setup Kafka backend mock to return FAILURE
            var mockFailingKafkaBackend = new Mock<IWebhookBackend>();
            mockFailingKafkaBackend.Setup(b => b.BackendType).Returns(WebhookBackendType.Kafka);
            mockFailingKafkaBackend.Setup(b => b.SendAsync(It.IsAny<Webhook>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WebhookBackendResult
                {
                    Success = false,
                    Message = "Kafka broker unreachable"
                });
            _mockBackendFactory.Setup(f => f.GetBackend(WebhookBackendType.Kafka))
                .Returns(mockFailingKafkaBackend.Object);

            var payload = new { message = "This will fail" };

            // Act - Submit webhook event
            var submitResult = await _submissioncontroller.PostWebhookEvent(failingKafkaWebhook.Owner, failingKafkaWebhook.Project, failingKafkaWebhook.Slug, payload);
            
            // Assert 1: Response should still be Created (async doesn't wait for failure)
            var actionResult = Assert.IsType<ActionResult<WebhookEventDto>>(submitResult);
            var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var webhookEventDto = Assert.IsType<WebhookEventDto>(createdResult.Value);
            
            // Assert 2: Wait for background task to process failure
            var eventId = webhookEventDto.Id;
            WebhookEvent? updatedEvent = null;
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(100);
                updatedEvent = await _context.WebhookEvents.FindAsync(eventId);
                if (updatedEvent?.Status == WebhookEventStatus.Processed && updatedEvent?.SubStatus == WebhookEventSubStatus.Failed)
                    break;
            }
            
            // Assert 3: Event should be marked as Processed with Failed substatus
            Assert.NotNull(updatedEvent);
            Assert.Equal(WebhookEventStatus.Processed, updatedEvent.Status);
            Assert.Equal(WebhookEventSubStatus.Failed, updatedEvent.SubStatus);
            Assert.Contains("Kafka broker unreachable", updatedEvent.StatusResultText ?? "");
        }

        // ===================================================================
        // PRIORITY 1 TESTS: Webhook Status Validation
        // ===================================================================

        [Fact]
        public async Task PostWebhookEvent_WithDisabledWebhook_ReturnsBadRequest()
        {
            // Arrange - Create a DISABLED webhook
            var disabledWebhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "DisabledWebhook",
                Slug = "disabled-webhook",
                Owner = "disabled-org",
                Project = "disabled-project",
                Status = WebhookStatus.Disabled, // KEY: Disabled status
                BackendType = WebhookBackendType.Database,
                DeliveryMode = WebhookDeliveryMode.Synchronous
            };
            _context.Webhooks.Add(disabledWebhook);
            _context.SaveChanges();

            var payload = new { message = "Should be rejected" };

            // Act - Attempt to submit event to disabled webhook
            var submitResult = await _submissioncontroller.PostWebhookEvent(disabledWebhook.Owner, disabledWebhook.Project, disabledWebhook.Slug, payload);
            
            // Assert 1: Should return BadRequest (400)
            var actionResult = Assert.IsType<ActionResult<WebhookEventDto>>(submitResult);
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
            
            // Assert 2: Error message should indicate webhook is disabled
            var errorMessage = badRequestResult.Value?.ToString();
            Assert.NotNull(errorMessage);
            Assert.Contains("disabled", errorMessage, StringComparison.OrdinalIgnoreCase);
            
            // Assert 3: No event should be created in database
            var eventCount = _context.WebhookEvents.Count(e => e.WebhookId == disabledWebhook.Id);
            Assert.Equal(0, eventCount);
            
            // Assert 4: LastReceivedAt should NOT be updated
            var webhookCheck = await _context.Webhooks.FindAsync(disabledWebhook.Id);
            Assert.Null(webhookCheck?.LastReceivedAt);
        }

        [Fact]
        public async Task PostWebhookEvent_WithSuspendedWebhook_ReturnsBadRequest()
        {
            // Arrange - Create a SUSPENDED webhook
            var suspendedWebhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "SuspendedWebhook",
                Slug = "suspended-webhook",
                Owner = "suspended-org",
                Project = "suspended-project",
                Status = WebhookStatus.Suspended, // KEY: Suspended status
                BackendType = WebhookBackendType.Kafka,
                DeliveryMode = WebhookDeliveryMode.Synchronous,
                BackendConfig = "{\"BootstrapServers\":\"localhost:9092\",\"Topic\":\"test-topic\"}"
            };
            _context.Webhooks.Add(suspendedWebhook);
            _context.SaveChanges();

            var payload = new { message = "Should also be rejected" };

            // Act - Attempt to submit event to suspended webhook
            var submitResult = await _submissioncontroller.PostWebhookEvent(suspendedWebhook.Owner, suspendedWebhook.Project, suspendedWebhook.Slug, payload);
            
            // Assert 1: Should return BadRequest (400)
            var actionResult = Assert.IsType<ActionResult<WebhookEventDto>>(submitResult);
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
            
            // Assert 2: Error message should indicate webhook is suspended
            var errorMessage = badRequestResult.Value?.ToString();
            Assert.NotNull(errorMessage);
            Assert.Contains("suspended", errorMessage, StringComparison.OrdinalIgnoreCase);
            
            // Assert 3: No event should be created in database
            var eventCount = _context.WebhookEvents.Count(e => e.WebhookId == suspendedWebhook.Id);
            Assert.Equal(0, eventCount);
            
            // Assert 4: Backend should NOT be called
            _mockBackendFactory.Verify(f => f.GetBackend(WebhookBackendType.Kafka), Times.Never);
            
            // Assert 5: LastReceivedAt should NOT be updated
            var webhookCheck = await _context.Webhooks.FindAsync(suspendedWebhook.Id);
            Assert.Null(webhookCheck?.LastReceivedAt);
        }

        // ===================================================================
        // NEGATIVE TEST CASES: Invalid Input & State
        // ===================================================================

        [Fact]
        public async Task PostWebhookEvent_WithNullPayload_ReturnsCreated()
        {
            // Arrange
            var webhook = _webhook; // Use seeded webhook

            // Act - Submit with null payload (should serialize to "null")
            var submitResult = await _submissioncontroller.PostWebhookEvent(webhook.Owner, webhook.Project, webhook.Slug, null);
            
            // Assert - Should still create event (null is valid JSON)
            var actionResult = Assert.IsType<ActionResult<WebhookEventDto>>(submitResult);
            var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var webhookEventDto = Assert.IsType<WebhookEventDto>(createdResult.Value);
            Assert.Equal(WebhookEventStatus.New, webhookEventDto.Status);
        }

        [Fact]
        public async Task PostWebhookEvent_WithNonExistentWebhook_ReturnsNotFound()
        {
            // Arrange
            var payload = new { message = "test" };

            // Act - Submit to non-existent webhook
            var submitResult = await _submissioncontroller.PostWebhookEvent("non-existent-org", "non-existent-project", "non-existent-slug", payload);
            
            // Assert
            var actionResult = Assert.IsType<ActionResult<WebhookEventDto>>(submitResult);
            Assert.IsType<NotFoundResult>(actionResult.Result);
        }

        [Fact]
        public async Task GetWebhookEvent_WithDisabledWebhook_ReturnsBadRequest()
        {
            // Arrange - Create disabled webhook
            var disabledWebhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "DisabledGetWebhook",
                Slug = "disabled-get-webhook",
                Owner = "disabled-get-org",
                Project = "disabled-get-project",
                Status = WebhookStatus.Disabled,
                BackendType = WebhookBackendType.Database,
                DeliveryMode = WebhookDeliveryMode.Synchronous
            };
            _context.Webhooks.Add(disabledWebhook);
            _context.SaveChanges();

            // Act - Attempt GET submission to disabled webhook
            var submitResult = await _submissioncontroller.GetWebhookEvent(disabledWebhook.Owner, disabledWebhook.Project, disabledWebhook.Slug);
            
            // Assert
            var actionResult = Assert.IsType<ActionResult<WebhookEventDto>>(submitResult);
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
            Assert.Contains("disabled", badRequestResult.Value?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AsyncDelivery_Database_WithBackendException_StaysNewForRetry()
        {
            // Arrange - Database backend that throws exception
            var exceptionWebhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "ExceptionWebhook",
                Slug = "exception-webhook",
                Owner = "exception-org",
                Project = "exception-project",
                Status = WebhookStatus.Enabled,
                BackendType = WebhookBackendType.Database,
                DeliveryMode = WebhookDeliveryMode.Asynchronous
            };
            _context.Webhooks.Add(exceptionWebhook);
            _context.SaveChanges();

            // Setup backend to throw exception
            var mockExceptionBackend = new Mock<IWebhookBackend>();
            mockExceptionBackend.Setup(b => b.BackendType).Returns(WebhookBackendType.Database);
            mockExceptionBackend.Setup(b => b.SendAsync(It.IsAny<Webhook>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Database connection failed"));
            _mockBackendFactory.Setup(f => f.GetBackend(WebhookBackendType.Database))
                .Returns(mockExceptionBackend.Object);

            var payload = new { message = "Exception test" };

            // Act
            var submitResult = await _submissioncontroller.PostWebhookEvent(exceptionWebhook.Owner, exceptionWebhook.Project, exceptionWebhook.Slug, payload);
            
            // Assert - Event should be created despite exception
            var actionResult = Assert.IsType<ActionResult<WebhookEventDto>>(submitResult);
            var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var webhookEventDto = Assert.IsType<WebhookEventDto>(createdResult.Value);
            
            // Wait for background task
            await Task.Delay(1000);
            
            // Database backend should keep status as New even on exception (for retry)
            var eventCheck = await _context.WebhookEvents.FindAsync(webhookEventDto.Id);
            Assert.NotNull(eventCheck);
            Assert.Equal(WebhookEventStatus.New, eventCheck.Status);
        }

    }
}
