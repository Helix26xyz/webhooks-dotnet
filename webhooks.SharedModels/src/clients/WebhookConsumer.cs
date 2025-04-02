using Microsoft.EntityFrameworkCore.Query;
using webhooks.SharedModels.models;
namespace webhooks.SharedModels.clients
{
    public class WebhookConsumer : IWebhookConsumer
    {
        private readonly WebhookEventsApiClient webhookEventHttpClient;
        private readonly WebhookApiClient webhookHttpClient;
        public string Name { get; set; }
        public Webhook Webhook { get; set; }
        public string Script { get; set; }

        public WebhookConsumer(WebhookEventsApiClient webhookEventHttpClient, WebhookApiClient webhookHttpClient, string name, Webhook webhook, string script)
        {
            this.webhookEventHttpClient = webhookEventHttpClient;
            this.webhookHttpClient = webhookHttpClient;
            Name = name;
            Webhook = webhook;
            Script = script;
        }
        public static async Task<WebhookConsumer> CreateAsync(WebhookEventsApiClient webhookEventHttpClient, WebhookApiClient webhookHttpClient, string name, string webhook, string script)
        {
            var _webhook = await webhookHttpClient.GetWebhookAsync(webhook);
            return new WebhookConsumer(webhookEventHttpClient, webhookHttpClient, name, _webhook, script);
        }

        private async Task<WebhookEvent?> GetNextWebhookEventAsync()
        {
            // call the webhook API to get the next webhook event at api/webhookevents/receive/{this.webhook}
            // return the webhook event
            var result = await webhookEventHttpClient.GetWebhookEventAsync(Webhook.Id);
            return result;

        }

        public async Task<WebhookEventWorkResponse> ProcessWebhookEventAsync(WebhookEvent webhookEvent)
        {
            Console.WriteLine($"Processing webhook event {webhookEvent.Id} for webhook {webhookEvent.WebhookId}");
            await Task.Delay(1000); // Simulate processing time

            var result = new WebhookEventWorkResponse
            {
                Status = WebhookEventSubStatus.Success,
                ResultText = "Webhook event processed successfully"
            };

            await webhookEventHttpClient.updateWebhookEventAsync(webhookEvent.Id, result);
            return result;
        }
        public async Task<WebhookEventWorkResponseCollection> StartProcessingLoopAsync()
        {
            var results = new WebhookEventWorkResponseCollection();
            // Start the processing loop.


            while (true)
            {
                var nextWebhook = await GetNextWebhookEventAsync();
                if (nextWebhook != null)
                {
                    // Process the webhook event.
                    var result = await ProcessWebhookEventAsync(nextWebhook);


                    // Update the results.
                    if (result.Status == WebhookEventSubStatus.Success)
                    {
                        results.ProcessedCount++;
                    }
                    else
                    {
                        results.FailedCount++;
                    }
                }
                // delay for 30s
                await Task.Delay(30000);

                if (results.TotalCount > 100)
                {
                    break;
                }
            }
            return results;


        }
    }
}
