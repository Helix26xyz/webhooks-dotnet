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
using webhooks.SharedModels.security;
using Xunit;
using webhooks.SharedModels.storage;
using webhooks.SharedModels.backends;


namespace webhooks.ApiService.Tests
{
    public class WebhooksControllerTests
    {
        private readonly AppDbContext _context;
        private readonly WebhooksController _controller;
        private readonly Mock<IEncryptionService> _mockEncryptionService;
        private readonly Mock<IWebhookBackendFactory> _mockBackendFactory;

        public WebhooksControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDatabase_{Guid.NewGuid()}")
                .Options;

            _context = new AppDbContext(options);
            
            // Setup mock encryption service
            _mockEncryptionService = new Mock<IEncryptionService>();
            _mockEncryptionService.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(s => s);
            _mockEncryptionService.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(s => s);
            _mockEncryptionService.Setup(e => e.IsEncrypted(It.IsAny<string>())).Returns(false);
            
            // Setup mock backend factory
            _mockBackendFactory = new Mock<IWebhookBackendFactory>();
            var mockBackend = new Mock<IWebhookBackend>();
            mockBackend.Setup(b => b.TestConnectionAsync(It.IsAny<Webhook>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _mockBackendFactory.Setup(f => f.GetBackend(It.IsAny<WebhookBackendType>())).Returns(mockBackend.Object);
            
            _controller = new WebhooksController(_context, _mockEncryptionService.Object, _mockBackendFactory.Object);

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
            var actionResult = Assert.IsType<ActionResult<IEnumerable<WebhookDto>>>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<WebhookDto>>(actionResult.Value);
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
            Assert.NotNull(result.Value);
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
            var actionResult = Assert.IsType<ActionResult<WebhookDto>>(result);
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
            var actionResult = Assert.IsType<ActionResult<WebhookDto>>(result);
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var model = Assert.IsAssignableFrom<WebhookDto>(createdAtActionResult.Value);
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
            Assert.NotNull(updatedWebhook);
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
