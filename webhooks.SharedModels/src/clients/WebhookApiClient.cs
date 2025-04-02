using webhooks.SharedModels.models;
using System.Net.Http.Json;
namespace webhooks.SharedModels.clients;

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
    public async Task<Webhook?> GetWebhookAsync(string webhookId, CancellationToken cancellationToken = default)
    {
        Webhook? webhook = await httpClient.GetFromJsonAsync<Webhook>($"/api/webhooks/{webhookId}", cancellationToken);

        return webhook;
    }
    public async Task<Webhook> AddWebhookAsync(Webhook newWebhook, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/webhooks", newWebhook, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Webhook>(cancellationToken: cancellationToken) ?? new Webhook();
    }
    public async Task<bool> DeleteWebhookAsync(Guid webhookId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"/api/webhooks/{webhookId}", cancellationToken);
        return response.IsSuccessStatusCode;
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
    public async Task<Webhook> CreateWebhookAsync(Webhook newWebhook, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/webhooks", newWebhook, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Webhook>(cancellationToken: cancellationToken) ?? new Webhook();
    }
    public async Task<bool> UpdateWebhookAsync(Webhook webhook, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/webhooks/{webhook.Id}", webhook, cancellationToken);
        response.EnsureSuccessStatusCode();
        return response.IsSuccessStatusCode;
    }
}