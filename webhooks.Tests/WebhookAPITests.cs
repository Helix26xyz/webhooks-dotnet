using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using webhooks.ApiService;
using webhooks.ApiService.src;
using webhooks.SharedModels.models;
using Xunit;
using webhooks.SharedModels.storage;


namespace webhooks.ApiService.Tests
{
    public class WebhooksControllerTests
    {
        private readonly AppDbContext _context;
        private readonly WebhooksController _controller;

        public WebhooksControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDatabase")
                .Options;

            _context = new AppDbContext(options);
            _controller = new WebhooksController(_context);

            // Seed the database with test data
            _context.Webhooks.AddRange(new List<Webhook>
                {
                    new Webhook { Id = Guid.NewGuid(), Name = "Webhook1" },
                    new Webhook { Id = Guid.NewGuid(), Name = "Webhook2" }
                });
            _context.SaveChanges();
        }

        [Fact]
        public async Task GetWebhooks_ReturnsAllWebhooks()
        {
            // Act
            var result = await _controller.GetWebhooks();

            // Assert
            var actionResult = Assert.IsType<ActionResult<IEnumerable<Webhook>>>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<Webhook>>(actionResult.Value);
            Assert.Equal(2, model.Count());
        }

        [Fact]
        public async Task GetWebhook_ReturnsWebhook()
        {
            // Arrange
            var webhookId = _context.Webhooks.First().Id;

            // Act
            var result = await _controller.GetWebhook(webhookId);

            // Assert
            //var actionResult = Assert.IsType<ActionResult<Webhook>>(result);
            //var model = Assert.IsAssignableFrom<IEnumerable<Webhook>>(actionResult.Value);
            Assert.Equal(webhookId, result.Value.Id);
        }

        [Fact]
        public async Task GetWebhook_ReturnsNotFound_WhenWebhookDoesNotExist()
        {
            // Arrange
            var webhookId = Guid.NewGuid();

            // Act
            var result = await _controller.GetWebhook(webhookId);

            // Assert
            var actionResult = Assert.IsType<ActionResult<Webhook>>(result);
            Assert.IsType<NotFoundResult>(actionResult.Result);
        }

        [Fact]
        public async Task PostWebhook_CreatesWebhook()
        {
            // Arrange
            var webhook = new Webhook { Id = Guid.NewGuid(), Name = "Webhook3" };
            var previousCount = _context.Webhooks.Count();
            // Act
            var result = await _controller.PostWebhook(webhook);

            // Assert
            var actionResult = Assert.IsType<ActionResult<Webhook>>(result);
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var model = Assert.IsAssignableFrom<Webhook>(createdAtActionResult.Value);
            Assert.Equal(webhook.Id, model.Id);
            Assert.Equal(previousCount + 1, _context.Webhooks.Count());
        }

        [Fact]
        public async Task PutWebhook_UpdatesWebhook()
        {
            // Arrange
            var webhookId = _context.Webhooks.First().Id;
            var webhook = new Webhook { Id = webhookId, Name = "UpdatedWebhook" };

            // Act
            var result = await _controller.PutWebhook(webhookId, webhook);

            // Assert
            Assert.IsType<NoContentResult>(result);
            var updatedWebhook = await _context.Webhooks.FindAsync(webhookId);
            Assert.Equal("UpdatedWebhook", updatedWebhook.Name);
        }

        [Fact]
        public async Task DeleteWebhook_DeletesWebhook()
        {
            // Arrange
            var webhookId = _context.Webhooks.First().Id;
            var previousCount = _context.Webhooks.Count();

            // Act
            var result = await _controller.DeleteWebhook(webhookId);

            // Assert
            Assert.IsType<NoContentResult>(result);
            Assert.Null(await _context.Webhooks.FindAsync(webhookId));
            Assert.Equal(previousCount - 1, _context.Webhooks.Count());
        }
    }
}
