using webhooks.SharedModels.models;
using System.Net.Http.Json;
namespace webhooks.SharedModels.clients;

public class WebhookEventsApiClient(HttpClient httpClient)
{
    public async Task<WebhookEvent?> GetWebhookEventAsync(Guid webhookId,CancellationToken cancellationToken = default)
    {

        var webhookEvent = await httpClient.GetFromJsonAsync<WebhookEvent>($"/api/webhookevents/receive/{webhookId.ToString()}", cancellationToken);


        return webhookEvent;
    }
    public async Task<bool> updateWebhookEventAsync(Guid webhookId, WebhookEventWorkResponse response, CancellationToken cancellationToken = default)
    {

        var res = await httpClient.PutAsJsonAsync<WebhookEventWorkResponse>($"/api/webhookevents/return/{webhookId.ToString()}", response, cancellationToken);



        return res != null;
    }
}