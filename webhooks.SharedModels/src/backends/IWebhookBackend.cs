using webhooks.SharedModels.models;

namespace webhooks.SharedModels.backends
{
    /// <summary>
    /// Interface for webhook backend implementations
    /// </summary>
    public interface IWebhookBackend
    {
        /// <summary>
        /// Send payload to the backend
        /// </summary>
        /// <param name="webhook">The webhook configuration</param>
        /// <param name="payload">The payload to send</param>
        /// <param name="webhookEventId">The webhook event ID for tracking</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Backend send result</returns>
        Task<WebhookBackendResult> SendAsync(Webhook webhook, string payload, Guid webhookEventId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Test backend connection/configuration
        /// </summary>
        /// <param name="webhook">The webhook configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if backend is accessible and properly configured</returns>
        Task<bool> TestConnectionAsync(Webhook webhook, CancellationToken cancellationToken = default);

        /// <summary>
        /// Backend type this implementation handles
        /// </summary>
        WebhookBackendType BackendType { get; }
    }

    public class WebhookBackendResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? BackendReference { get; set; } // e.g., Kafka offset, queue message ID
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Exception? Exception { get; set; }
    }
}
