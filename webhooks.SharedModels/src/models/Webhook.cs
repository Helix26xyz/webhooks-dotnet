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
    }

    public enum WebhookStatus
    {
        Enabled = 1,
        Disabled = 2,
        Suspended = 3
    }
    }
