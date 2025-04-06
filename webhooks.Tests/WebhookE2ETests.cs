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

        private Guid _webhookId;
        private Guid _webhookEventId;
        public WebhookE2ETests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDatabase_{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context = new AppDbContext(options);
            _webhookcontroller = new WebhooksController(_context);
            _eventcontroller = new WebhookEventsController(_context);
            _submissioncontroller = new WebhookEventsSubmissionController(_context);

            _webhookId = Guid.NewGuid();
            // Seed the database with test data
            _webhook = new Webhook
            {
                Id = _webhookId,
                Name = $"TestWebhook{_webhookId}",
                Slug = $"test-webhook{_webhookId}",
                Owner = $"test-org{_webhookId}",
                Project = $"test-project{_webhookId}",
                Status = WebhookStatus.Enabled
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
            Assert.Equal(WebhookEventStatus.New, webhookEvent.Status);
            Assert.Equal(WebhookEventSubStatus.Pending, webhookEvent.SubStatus);
            Assert.Contains("Test payload", webhookEvent.Payload);

            // Act
            var result2 = await _eventcontroller.ReceiveWebhookEvent(_webhook.Id);
            var objectResult2 = result2.Result as ObjectResult;
            var thisWebhookEvent2 = objectResult2?.Value as WebhookEvent;

            // Assert
            Assert.Equal(thisWebhookEvent2.Id, _webhookEvent.Id);
            // Act
            var result3 = await _eventcontroller.ReceiveWebhookEvent(_webhook.Id);

            var objectResult3 = result3.Result as ObjectResult;
            Assert.Equal(objectResult3, null);
        }

    }
}
