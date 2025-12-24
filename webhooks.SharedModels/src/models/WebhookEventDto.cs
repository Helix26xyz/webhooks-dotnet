namespace webhooks.SharedModels.models
{
    /// <summary>
    /// DTO for webhook event submission responses - simplified view without sensitive data
    /// </summary>
    public class WebhookEventDto
    {
        public Guid Id { get; set; }
        public Guid WebhookId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public WebhookEventStatus Status { get; set; }
        public WebhookEventSubStatus SubStatus { get; set; }
        public WebhookSubmissionInfo? Webhook { get; set; }

        public static WebhookEventDto FromWebhookEvent(WebhookEvent webhookEvent)
        {
            return new WebhookEventDto
            {
                Id = webhookEvent.Id,
                WebhookId = webhookEvent.WebhookId,
                CreatedAt = webhookEvent.CreatedAt,
                UpdatedAt = webhookEvent.UpdatedAt,
                Status = webhookEvent.Status,
                SubStatus = webhookEvent.SubStatus,
                Webhook = webhookEvent.Webhook != null 
                    ? WebhookSubmissionInfo.FromWebhook(webhookEvent.Webhook)
                    : null
            };
        }
    }

    /// <summary>
    /// Simplified webhook info for submission responses
    /// </summary>
    public class WebhookSubmissionInfo
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;

        public static WebhookSubmissionInfo FromWebhook(Webhook webhook)
        {
            return new WebhookSubmissionInfo
            {
                Id = webhook.Id,
                Name = webhook.Name,
                Slug = webhook.Slug,
                Url = webhook.Url,
                Owner = webhook.Owner,
                Project = webhook.Project
            };
        }
    }
}
