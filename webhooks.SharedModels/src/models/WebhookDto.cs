namespace webhooks.SharedModels.models
{
    /// <summary>
    /// DTO for webhook responses - excludes sensitive backend configuration
    /// </summary>
    public class WebhookDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public WebhookStatus Status { get; set; }
        
        // Backend Configuration - Type is exposed, but not the config itself
        public WebhookBackendType BackendType { get; set; }
        public WebhookDeliveryMode DeliveryMode { get; set; }
        public DateTime? LastReceivedAt { get; set; }

        public static WebhookDto FromWebhook(Webhook webhook)
        {
            return new WebhookDto
            {
                Id = webhook.Id,
                Name = webhook.Name,
                Slug = webhook.Slug,
                Url = webhook.Url,
                CreatedAt = webhook.CreatedAt,
                CreatedBy = webhook.CreatedBy,
                Owner = webhook.Owner,
                Project = webhook.Project,
                Status = webhook.Status,
                BackendType = webhook.BackendType,
                DeliveryMode = webhook.DeliveryMode,
                LastReceivedAt = webhook.LastReceivedAt
            };
        }
    }
}
