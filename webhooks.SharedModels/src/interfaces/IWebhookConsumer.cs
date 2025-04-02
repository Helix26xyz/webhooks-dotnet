namespace webhooks.SharedModels.models
{
    public interface IWebhookConsumer
    {
        public Task<WebhookEventWorkResponse> ProcessWebhookEventAsync(WebhookEvent webhookEvent);
        public string Name { get; }
        public Webhook Webhook { get; }
        public string Script { get; }
    }
}
