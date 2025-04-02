using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using webhooks.ApiService.src;
using webhooks.SharedModels.models;
using webhooks.SharedModels.storage;
using Xunit;

namespace webhooks.Tests
{
    public class WebhooksControllerTest
    {
        private readonly Mock<AppDbContext> _mockContext;
        private readonly WebhooksController _controller;
        private readonly List<Webhook> _webhooks;

        public WebhooksControllerTest()
        {
            _mockContext = new Mock<AppDbContext>();
            _controller = new WebhooksController(_mockContext.Object);

            _webhooks = new List<Webhook>
            {
                new Webhook { Id = Guid.NewGuid(), Url = "http://example.com/1" },
                new Webhook { Id = Guid.NewGuid(), Url = "http://example.com/2" }
            };

            var mockSet = new Mock<DbSet<Webhook>>();
            mockSet.As<IQueryable<Webhook>>().Setup(m => m.Provider).Returns(_webhooks.AsQueryable().Provider);
            mockSet.As<IQueryable<Webhook>>().Setup(m => m.Expression).Returns(_webhooks.AsQueryable().Expression);
            mockSet.As<IQueryable<Webhook>>().Setup(m => m.ElementType).Returns(_webhooks.AsQueryable().ElementType);
            mockSet.As<IQueryable<Webhook>>().Setup(m => m.GetEnumerator()).Returns(_webhooks.AsQueryable().GetEnumerator());

            _mockContext.Setup(c => c.Webhooks).Returns(mockSet.Object);
        }

        [Fact]
        public async Task GetWebhooks_ReturnsAllWebhooks()
        {
            var result = await _controller.GetWebhooks();

            var actionResult = Assert.IsType<ActionResult<IEnumerable<Webhook>>>(result);
            var returnValue = Assert.IsType<List<Webhook>>(actionResult.Value);
            Assert.Equal(2, returnValue.Count);
        }

        [Fact]
        public async Task GetWebhook_ReturnsWebhook()
        {
            var webhookId = _webhooks[0].Id;
            _mockContext.Setup(c => c.Webhooks.FindAsync(webhookId)).ReturnsAsync(_webhooks[0]);

            var result = await _controller.GetWebhook(webhookId);

            var actionResult = Assert.IsType<ActionResult<Webhook>>(result);
            var returnValue = Assert.IsType<Webhook>(actionResult.Value);
            Assert.Equal(webhookId, returnValue.Id);
        }

        [Fact]
        public async Task GetWebhook_ReturnsNotFound()
        {
            var webhookId = Guid.NewGuid();
            _mockContext.Setup(c => c.Webhooks.FindAsync(webhookId)).ReturnsAsync((Webhook)null);

            var result = await _controller.GetWebhook(webhookId);

            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task PostWebhook_CreatesWebhook()
        {
            var newWebhook = new Webhook { Id = Guid.NewGuid(), Url = "http://example.com/3" };

            var result = await _controller.PostWebhook(newWebhook);

            var actionResult = Assert.IsType<ActionResult<Webhook>>(result);
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var returnValue = Assert.IsType<Webhook>(createdAtActionResult.Value);
            Assert.Equal(newWebhook.Id, returnValue.Id);
        }

        [Fact]
        public async Task PutWebhook_UpdatesWebhook()
        {
            var webhookId = _webhooks[0].Id;
            var updatedWebhook = new Webhook { Id = webhookId, Url = "http://example.com/updated" };

            _mockContext.Setup(c => c.Webhooks.FindAsync(webhookId)).ReturnsAsync(_webhooks[0]);

            var result = await _controller.PutWebhook(webhookId, updatedWebhook);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task PutWebhook_ReturnsBadRequest()
        {
            var webhookId = _webhooks[0].Id;
            var updatedWebhook = new Webhook { Id = Guid.NewGuid(), Url = "http://example.com/updated" };

            var result = await _controller.PutWebhook(webhookId, updatedWebhook);

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task DeleteWebhook_DeletesWebhook()
        {
            var webhookId = _webhooks[0].Id;
            _mockContext.Setup(c => c.Webhooks.FindAsync(webhookId)).ReturnsAsync(_webhooks[0]);

            var result = await _controller.DeleteWebhook(webhookId);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteWebhook_ReturnsNotFound()
        {
            var webhookId = Guid.NewGuid();
            _mockContext.Setup(c => c.Webhooks.FindAsync(webhookId)).ReturnsAsync((Webhook)null);

            var result = await _controller.DeleteWebhook(webhookId);

            Assert.IsType<NotFoundResult>(result);
        }
    }
}