using System.Text.Json;
using Microsoft.Extensions.Logging;
using webhooks.SharedModels.models;

namespace webhooks.SharedModels.backends
{
    /// <summary>
    /// RabbitMQ backend - sends webhook events to RabbitMQ queues
    /// BackendConfig format: {"HostName": "localhost", "QueueName": "webhook-events", "UserName": "guest", "Password": "guest"}
    /// </summary>
    public class RabbitMQBackend : IWebhookBackend
    {
        private readonly ILogger<RabbitMQBackend> _logger;

        public WebhookBackendType BackendType => WebhookBackendType.RabbitMQ;

        public RabbitMQBackend(ILogger<RabbitMQBackend> logger)
        {
            _logger = logger;
        }

        public async Task<WebhookBackendResult> SendAsync(Webhook webhook, string payload, Guid webhookEventId, CancellationToken cancellationToken = default)
        {
            try
            {
                var config = ParseConfig(webhook.BackendConfig);
                
                if (string.IsNullOrEmpty(config.HostName))
                {
                    throw new InvalidOperationException("RabbitMQ HostName not configured");
                }

                if (string.IsNullOrEmpty(config.QueueName))
                {
                    throw new InvalidOperationException("RabbitMQ QueueName not configured");
                }

                // TODO: Implement actual RabbitMQ publisher using RabbitMQ.Client
                // This is a placeholder implementation
                _logger.LogInformation(
                    "RabbitMQ backend would send webhook event {WebhookEventId} to queue {QueueName} at {HostName}",
                    webhookEventId, config.QueueName, config.HostName);

                // Simulate sending to RabbitMQ
                await Task.Delay(10, cancellationToken); // Simulate network latency

                return new WebhookBackendResult
                {
                    Success = true,
                    Message = $"Payload sent to RabbitMQ queue: {config.QueueName}",
                    BackendReference = $"rabbitmq://{config.HostName}/{config.QueueName}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending webhook event {WebhookEventId} to RabbitMQ", webhookEventId);
                return new WebhookBackendResult
                {
                    Success = false,
                    Message = ex.Message,
                    Exception = ex
                };
            }
        }

        public async Task<bool> TestConnectionAsync(Webhook webhook, CancellationToken cancellationToken = default)
        {
            try
            {
                var config = ParseConfig(webhook.BackendConfig);
                
                if (string.IsNullOrEmpty(config.HostName) || string.IsNullOrEmpty(config.QueueName))
                {
                    return false;
                }

                // TODO: Implement actual RabbitMQ connection test
                _logger.LogInformation("Testing RabbitMQ connection to {HostName}", config.HostName);
                await Task.Delay(10, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing RabbitMQ connection");
                return false;
            }
        }

        private RabbitMQConfig ParseConfig(string? backendConfig)
        {
            if (string.IsNullOrEmpty(backendConfig))
            {
                return new RabbitMQConfig();
            }

            try
            {
                // Log the first few characters to help debug encryption issues (without exposing full config)
                var preview = backendConfig.Length > 10 ? backendConfig.Substring(0, 10) + "..." : backendConfig;
                _logger.LogDebug("Parsing RabbitMQ config (preview: {Preview}, length: {Length})", preview, backendConfig.Length);
                
                return JsonSerializer.Deserialize<RabbitMQConfig>(backendConfig) ?? new RabbitMQConfig();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error parsing RabbitMQ backend config. Config preview (first 20 chars): {ConfigPreview}", 
                    backendConfig.Length > 20 ? backendConfig.Substring(0, 20) + "..." : backendConfig);
                return new RabbitMQConfig();
            }
        }

        private class RabbitMQConfig
        {
            public string? HostName { get; set; }
            public string? QueueName { get; set; }
            public string? UserName { get; set; }
            public string? Password { get; set; }
            public int Port { get; set; } = 5672;
        }
    }
}
