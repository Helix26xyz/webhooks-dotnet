using webhooks.SharedModels.models;
using System.Net.Http.Json;
namespace webhooks.SharedModels.clients;

public class WebhookEventsApiClient(HttpClient httpClient)
{
    public async Task<WebhookEvent?> GetWebhookEventAsync(Guid webhookId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/webhookevents/receive/{webhookId.ToString()}", cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            // Return null if the server responds with 204 No Content
            return null;
        }

        // Ensure the response is successful, or throw an exception
        response.EnsureSuccessStatusCode();

        // Deserialize the response content into a WebhookEvent
        var webhookEvent = await response.Content.ReadFromJsonAsync<WebhookEvent>(cancellationToken: cancellationToken);

        return webhookEvent;
    }

    public async Task<bool> updateWebhookEventAsync(Guid webhookId, WebhookEventWorkResponse response, CancellationToken cancellationToken = default)
    {
        var res = await httpClient.PutAsJsonAsync<WebhookEventWorkResponse>($"/api/webhookevents/return/{webhookId.ToString()}", response, cancellationToken);



        return res != null;
    }
}