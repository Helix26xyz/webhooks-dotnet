using System.Text.Json;
using Microsoft.Extensions.Logging;
using webhooks.SharedModels.models;
using Confluent.Kafka;

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
            _logger.LogInformation(
                "KafkaBackend.SendAsync called for webhook {WebhookId} ({WebhookName}), event {WebhookEventId}",
                webhook.Id, webhook.Name, webhookEventId);
            
            try
            {
                var config = ParseConfig(webhook.BackendConfig);
                
                _logger.LogInformation(
                    "Kafka config parsed: BootstrapServers={BootstrapServers}, Topic={Topic}",
                    config.BootstrapServers ?? "(null)", config.Topic ?? "(null)");
                
                if (string.IsNullOrEmpty(config.BootstrapServers))
                {
                    throw new InvalidOperationException("Kafka BootstrapServers not configured");
                }

                if (string.IsNullOrEmpty(config.Topic))
                {
                    throw new InvalidOperationException("Kafka Topic not configured");
                }

                _logger.LogInformation(
                    "Creating Kafka producer for {BootstrapServers}...",
                    config.BootstrapServers);

                // Create Kafka producer configuration
                var producerConfig = new ProducerConfig
                {
                    BootstrapServers = config.BootstrapServers,
                    ClientId = $"webhooks-{webhook.Id}",
                    // Reliability settings
                    Acks = Acks.Leader,
                    MessageTimeoutMs = 10000,
                    RequestTimeoutMs = 5000,
                    // Retry settings
                    MessageSendMaxRetries = 3,
                    RetryBackoffMs = 100
                };

                // Build the message with metadata
                var kafkaMessage = new Message<string, string>
                {
                    Key = webhookEventId.ToString(),
                    Value = payload,
                    Headers = new Headers
                    {
                        { "webhook-id", System.Text.Encoding.UTF8.GetBytes(webhook.Id.ToString()) },
                        { "webhook-name", System.Text.Encoding.UTF8.GetBytes(webhook.Name) },
                        { "webhook-event-id", System.Text.Encoding.UTF8.GetBytes(webhookEventId.ToString()) },
                        { "timestamp", System.Text.Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("o")) }
                    }
                };

                // Send to Kafka
                using var producer = new ProducerBuilder<string, string>(producerConfig).Build();
                
                var deliveryResult = await producer.ProduceAsync(config.Topic, kafkaMessage, cancellationToken);
                
                _logger.LogInformation(
                    "Successfully sent webhook event {WebhookEventId} to Kafka topic {Topic} at {BootstrapServers}, partition {Partition}, offset {Offset}",
                    webhookEventId, config.Topic, config.BootstrapServers, deliveryResult.Partition.Value, deliveryResult.Offset.Value);

                return new WebhookBackendResult
                {
                    Success = true,
                    Message = $"Message delivered to partition {deliveryResult.Partition.Value}, offset {deliveryResult.Offset.Value}",
                    BackendReference = $"kafka://{config.BootstrapServers}/{config.Topic}/partition:{deliveryResult.Partition.Value}/offset:{deliveryResult.Offset.Value}"
                };
            }
            catch (ProduceException<string, string> ex)
            {
                _logger.LogError(ex, "Kafka producer error sending webhook event {WebhookEventId}. Error: {ErrorReason}", 
                    webhookEventId, ex.Error.Reason);
                return new WebhookBackendResult
                {
                    Success = false,
                    Message = $"Kafka error: {ex.Error.Reason}",
                    Exception = ex
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
                    _logger.LogWarning("Kafka config validation failed: BootstrapServers or Topic is missing");
                    return false;
                }

                // Test connection by creating an admin client and fetching metadata
                var adminConfig = new AdminClientConfig
                {
                    BootstrapServers = config.BootstrapServers,
                    SocketTimeoutMs = 5000
                };

                using var adminClient = new AdminClientBuilder(adminConfig).Build();
                
                // Fetch cluster metadata with a short timeout
                var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));
                
                _logger.LogInformation(
                    "Successfully connected to Kafka at {BootstrapServers}. Cluster has {BrokerCount} broker(s), {TopicCount} topic(s)",
                    config.BootstrapServers, metadata.Brokers.Count, metadata.Topics.Count);
                
                // Check if the specified topic exists
                var topicExists = metadata.Topics.Any(t => t.Topic == config.Topic);
                if (!topicExists)
                {
                    _logger.LogWarning(
                        "Topic {Topic} does not exist on Kafka cluster. Available topics: {Topics}",
                        config.Topic, string.Join(", ", metadata.Topics.Select(t => t.Topic)));
                }
                
                return true;
            }
            catch (KafkaException ex)
            {
                _logger.LogError(ex, "Kafka connection test failed: {ErrorReason}", ex.Error.Reason);
                return false;
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
