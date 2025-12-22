using Microsoft.Extensions.Logging;
using webhooks.SharedModels.models;

namespace webhooks.SharedModels.backends
{
    /// <summary>
    /// Database backend - stores webhook events in the database (current/default behavior)
    /// </summary>
    public class DatabaseBackend : IWebhookBackend
    {
        private readonly ILogger<DatabaseBackend> _logger;

        public WebhookBackendType BackendType => WebhookBackendType.Database;

        public DatabaseBackend(ILogger<DatabaseBackend> logger)
        {
            _logger = logger;
        }

        public async Task<WebhookBackendResult> SendAsync(Webhook webhook, string payload, Guid webhookEventId, CancellationToken cancellationToken = default)
        {
            try
            {
                // For database backend, the payload is already saved by the controller
                // This backend just confirms the operation
                _logger.LogInformation("Database backend processed webhook event {WebhookEventId} for webhook {WebhookId}", 
                    webhookEventId, webhook.Id);

                return await Task.FromResult(new WebhookBackendResult
                {
                    Success = true,
                    Message = "Payload stored in database",
                    BackendReference = webhookEventId.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook event {WebhookEventId} in database backend", webhookEventId);
                return new WebhookBackendResult
                {
                    Success = false,
                    Message = ex.Message,
                    Exception = ex
                };
            }
        }

        public Task<bool> TestConnectionAsync(Webhook webhook, CancellationToken cancellationToken = default)
        {
            // Database connection is handled by EF Core context
            return Task.FromResult(true);
        }
    }
}
