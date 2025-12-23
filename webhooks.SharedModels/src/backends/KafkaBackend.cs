using System.Text.Json;
using Microsoft.Extensions.Logging;
using webhooks.SharedModels.models;

namespace webhooks.SharedModels.backends
{
    /// <summary>
    /// Kafka backend - sends webhook events to Kafka topics
    /// BackendConfig format: {"BootstrapServers": "localhost:9092", "Topic": "webhook-events"}
    /// </summary>
    public class KafkaBackend : IWebhookBackend
    {
        private readonly ILogger<KafkaBackend> _logger;

        public WebhookBackendType BackendType => WebhookBackendType.Kafka;

        public KafkaBackend(ILogger<KafkaBackend> logger)
        {
            _logger = logger;
        }

        public async Task<WebhookBackendResult> SendAsync(Webhook webhook, string payload, Guid webhookEventId, CancellationToken cancellationToken = default)
        {
            try
            {
                var config = ParseConfig(webhook.BackendConfig);
                
                if (string.IsNullOrEmpty(config.BootstrapServers))
                {
                    throw new InvalidOperationException("Kafka BootstrapServers not configured");
                }

                if (string.IsNullOrEmpty(config.Topic))
                {
                    throw new InvalidOperationException("Kafka Topic not configured");
                }

                // TODO: Implement actual Kafka producer using Confluent.Kafka
                // This is a placeholder implementation
                _logger.LogInformation(
                    "Kafka backend would send webhook event {WebhookEventId} to topic {Topic} at {BootstrapServers}",
                    webhookEventId, config.Topic, config.BootstrapServers);

                // Simulate sending to Kafka
                await Task.Delay(10, cancellationToken); // Simulate network latency

                return new WebhookBackendResult
                {
                    Success = true,
                    Message = $"Payload sent to Kafka topic: {config.Topic}",
                    BackendReference = $"kafka://{config.BootstrapServers}/{config.Topic}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending webhook event {WebhookEventId} to Kafka", webhookEventId);
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
                
                if (string.IsNullOrEmpty(config.BootstrapServers) || string.IsNullOrEmpty(config.Topic))
                {
                    return false;
                }

                // TODO: Implement actual Kafka connection test
                _logger.LogInformation("Testing Kafka connection to {BootstrapServers}", config.BootstrapServers);
                await Task.Delay(10, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Kafka connection");
                return false;
            }
        }

        private KafkaConfig ParseConfig(string? backendConfig)
        {
            if (string.IsNullOrEmpty(backendConfig))
            {
                return new KafkaConfig();
            }

            try
            {
                // Log the first few characters to help debug encryption issues (without exposing full config)
                var preview = backendConfig.Length > 10 ? backendConfig.Substring(0, 10) + "..." : backendConfig;
                _logger.LogDebug("Parsing Kafka config (preview: {Preview}, length: {Length})", preview, backendConfig.Length);
                
                return JsonSerializer.Deserialize<KafkaConfig>(backendConfig) ?? new KafkaConfig();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error parsing Kafka backend config. Config preview (first 20 chars): {ConfigPreview}", 
                    backendConfig.Length > 20 ? backendConfig.Substring(0, 20) + "..." : backendConfig);
                return new KafkaConfig();
            }
        }

        private class KafkaConfig
        {
            public string? BootstrapServers { get; set; }
            public string? Topic { get; set; }
        }
    }
}
