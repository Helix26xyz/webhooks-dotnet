namespace webhooks.ApiService.src.models


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
    }

    public class WebhookEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid WebhookId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string Payload { get; set; } = string.Empty;
        public required Webhook Webhook { get; set; }
        public WebhookEventStatus Status { get; set; }
        public WebhookEventSubStatus SubStatus { get; set; }

    }



    public enum WebhookStatus
    {
        Enabled = 1,
        Disabled = 2,
        Suspended = 3
    }

    public enum WebhookEventStatus
    {
        New = 1,
        Received = 2,
        Processed = 3
    }

    public class WebhookEventStatusEntity
    {
        public int Id { get; set; }
        public WebhookEventStatus Status { get; set; }
    }

    public enum WebhookEventSubStatus
    {
        Pending = 0,
            Success = 1,
            Failed = 2,
            Retry = 3,
            Skipped = 4 
    }

    public class WebhookEventSubStatusEntity
    {
        public int Id { get; set; }
        public WebhookEventSubStatus Status { get; set; }
    }
}
