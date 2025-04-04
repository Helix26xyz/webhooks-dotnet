using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using webhooks.ApiService.src;
using webhooks.SharedModels.models;
using webhooks.SharedModels.storage;
using Xunit;

namespace webhooks.ApiService.Tests
{
    public class WebhookSubmissionEventAPITests
    {
        private readonly AppDbContext _context;
        private readonly WebhookEventsSubmissionController _controller;

        public WebhookSubmissionEventAPITests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDatabase")
                .Options;

            _context = new AppDbContext(options);
            _controller = new WebhookEventsSubmissionController(_context);

            // Seed the database with test data
            var webhook = new Webhook
            {
                Id = Guid.NewGuid(),
                Name = "TestWebhook",
                Slug = "test-webhook",
                Owner = "test-org",
                Project = "test-project",
                Status = WebhookStatus.Enabled
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
            Assert.Equal(WebhookEventStatus.New, webhookEvent.Status);
            Assert.Equal(WebhookEventSubStatus.Pending, webhookEvent.SubStatus);
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
