namespace webhooks.SharedModels.models
{
    public class Webhook: IWebhook
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public WebhookStatus Status { get; set; }
        
        // Backend Configuration
        public WebhookBackendType BackendType { get; set; } = WebhookBackendType.Database;
        public string? BackendConfig { get; set; } // JSON config (connection strings, etc.) - Secret
        public WebhookDeliveryMode DeliveryMode { get; set; } = WebhookDeliveryMode.Synchronous;
        public DateTime? LastReceivedAt { get; set; }
    }

    public enum WebhookStatus
    {
        Enabled = 1,
        Disabled = 2,
        Suspended = 3
    }

    public enum WebhookBackendType
    {
        Database = 1,
        Kafka = 2,
        RabbitMQ = 3,
        AzureServiceBus = 4,
        AWSSQS = 5,
        Redis = 6,
        Custom = 99
    }

    public enum WebhookDeliveryMode
    {
        Synchronous = 1,      // Wait for backend confirmation
        Asynchronous = 2      // Fire-and-forget
    }
    }
