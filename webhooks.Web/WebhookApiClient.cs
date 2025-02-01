using webhooks.ApiService.src.models;
using webhooks.ApiService.src;

namespace webhooks.Web;

public class WebhookApiClient(HttpClient httpClient)
{
    public async Task<Webhook[]> GetWebhooksAsync(int maxItems = 10, CancellationToken cancellationToken = default)
    {
        List<Webhook>? webhooks = null;

        await foreach (var webhook in httpClient.GetFromJsonAsAsyncEnumerable<Webhook>("/api/webhooks", cancellationToken))
        {
            if (webhooks?.Count >= maxItems)
            {
                break;
            }
            if (webhook is not null)
            {
                webhooks ??= [];
                webhooks.Add((Webhook)webhook);
            }
        }

        return webhooks?.ToArray() ?? [];
    }
    public async Task<WebhookEvent[]> GetWebhookEventsForWebhookAsync(string webhookId,  int maxItems = 10, CancellationToken cancellationToken = default)
    {
        List<WebhookEvent>? webhookevents = null;

        await foreach (var webhookevent in httpClient.GetFromJsonAsAsyncEnumerable<WebhookEvent>($"/api/webhookevents/bywebhook/{webhookId}", cancellationToken))
        {
            if (webhookevents?.Count >= maxItems)
            {
                break;
            }
            if (webhookevent is not null)
            {
                webhookevents ??= [];
                webhookevents.Add((WebhookEvent) webhookevent);
            }
        }

        return webhookevents?.ToArray() ?? [];
    }
}