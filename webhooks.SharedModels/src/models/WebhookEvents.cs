namespace webhooks.SharedModels.models


{
    public class WebhookEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid WebhookId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? Payload { get; set; }
        public required Webhook Webhook { get; set; }
        public WebhookEventStatus Status { get; set; }
        public WebhookEventSubStatus SubStatus { get; set; }
        public string? StatusResultText { get; set; }
    }

    public class WebhookEventWorkResponse
    {
        public WebhookEventSubStatus Status { get; set; }
        public string? ResultText { get; set; }
    }

    public class WebhookEventWorkResponseCollection{
        public int TotalCount { get; set; }
        public int ProcessedCount { get; set; }
        public int FailedCount { get; set; }
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
