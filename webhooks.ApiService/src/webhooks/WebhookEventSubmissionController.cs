using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using webhooks.SharedModels.models;
using System.Threading.Tasks;
using webhooks.SharedModels.storage;
using webhooks.SharedModels.backends;
using webhooks.SharedModels.security;

namespace webhooks.ApiService.src
{
    [Route("api/wes")]
    [ApiController]
    public class WebhookEventsSubmissionController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebhookBackendFactory _backendFactory;
        private readonly ILogger<WebhookEventsSubmissionController> _logger;
        private readonly IEncryptionService _encryptionService;

        public WebhookEventsSubmissionController(
            AppDbContext context, 
            IWebhookBackendFactory backendFactory,
            ILogger<WebhookEventsSubmissionController> logger,
            IEncryptionService encryptionService)
        {
            _context = context;
            _backendFactory = backendFactory;
            _logger = logger;
            _encryptionService = encryptionService;
        }

        /// <summary>
        /// Process webhook event and send to configured backend
        /// </summary>
        private async Task<WebhookEvent> ProcessWebhookEventAsync(Webhook webhook, string serializedPayload)
        {
            // Update last received timestamp
            webhook.LastReceivedAt = DateTime.UtcNow;

            // Create webhook event for metadata tracking (always stored in DB)
            var webhookEvent = new WebhookEvent
            {
                Payload = serializedPayload,
                Status = WebhookEventStatus.New,
                SubStatus = WebhookEventSubStatus.Pending,
                Webhook = webhook
            };

            _context.WebhookEvents.Add(webhookEvent);
            await _context.SaveChangesAsync();

            // Send to configured backend
            try
            {
                var backend = _backendFactory.GetBackend(webhook.BackendType);
                
                _logger.LogInformation(
                    "Sending webhook event {WebhookEventId} to {BackendType} backend for webhook {WebhookId} ({WebhookName})",
                    webhookEvent.Id, webhook.BackendType, webhook.Id, webhook.Name);
                
                // Decrypt BackendConfig for backend use only
                var webhookForBackend = webhook.GetWebhookForBackend(_encryptionService);
                
                if (webhook.DeliveryMode == WebhookDeliveryMode.Synchronous)
                {
                    // Wait for backend to process (use decrypted webhook)
                    var backendResult = await backend.SendAsync(webhookForBackend, serializedPayload, webhookEvent.Id);
                    
                    _logger.LogInformation(
                        "Backend {BackendType} returned {Success} for webhook event {WebhookEventId}: {Message}",
                        webhook.BackendType, backendResult.Success, webhookEvent.Id, backendResult.Message);
                    
                    // For Database backend, keep status as New (event awaits consumer retrieval via /receive endpoint)
                    // For other backends (Kafka, etc.), mark as Processed since message was delivered
                    if (webhook.BackendType != WebhookBackendType.Database)
                    {
                        webhookEvent.Status = WebhookEventStatus.Processed;
                        webhookEvent.SubStatus = backendResult.Success ? WebhookEventSubStatus.Success : WebhookEventSubStatus.Failed;
                        webhookEvent.StatusResultText = backendResult.Message;
                        
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    // Fire-and-forget: Send to backend asynchronously
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var result = await backend.SendAsync(webhookForBackend, serializedPayload, webhookEvent.Id);
                            
                            // For Database backend, keep status as New (event awaits consumer retrieval)
                            // For other backends, update event status in background
                            if (webhookForBackend.BackendType != WebhookBackendType.Database)
                            {
                                using var scope = HttpContext.RequestServices.CreateScope();
                                var bgContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                                var bgEvent = await bgContext.WebhookEvents.FindAsync(webhookEvent.Id);
                                
                                if (bgEvent != null)
                                {
                                    bgEvent.Status = WebhookEventStatus.Processed;
                                    bgEvent.SubStatus = result.Success ? WebhookEventSubStatus.Success : WebhookEventSubStatus.Failed;
                                    bgEvent.StatusResultText = result.Message;
                                    await bgContext.SaveChangesAsync();
                                }
                            }
                        }
                        catch (Exception bgEx)
                        {
                            _logger.LogError(bgEx, "Background backend processing failed for webhook event {WebhookEventId}", webhookEvent.Id);
                        }
                    });
                }
            }
            catch (Exception backendEx)
            {
                _logger.LogError(backendEx, "Backend processing failed for webhook event {WebhookEventId}", webhookEvent.Id);
                
                // For Database backend, keep status as New even on error (consumer can retry)
                // For other backends, mark as Processed/Failed
                if (webhook.BackendType != WebhookBackendType.Database)
                {
                    webhookEvent.Status = WebhookEventStatus.Processed;
                    webhookEvent.SubStatus = WebhookEventSubStatus.Failed;
                    webhookEvent.StatusResultText = $"Backend error: {backendEx.Message}";
                    await _context.SaveChangesAsync();
                }
            }

            return webhookEvent;
        }

        /// <summary>
        /// Find webhook by organization, project, and slug
        /// </summary>
        private async Task<Webhook?> FindWebhookAsync(string org, string project, string webhookSlug)
        {
            return await _context.Webhooks.FirstOrDefaultAsync(w => 
                w.Slug == webhookSlug &&
                w.Owner == org &&
                w.Project == project &&
                w.Status != WebhookStatus.Disabled
            );
        }

        // GET: api/wes/:org/:project/:webhookSlug
        [HttpGet("{org}/{project}/{webhookSlug}")]
        public async Task<ActionResult<WebhookEventDto>> GetWebhookEvent(String org, String project, String webhookSlug)
        {
            try
            {
                _logger.LogInformation(
                    "Received webhook GET request for {Owner}/{Project}/{Slug}",
                    org, project, webhookSlug);
                
                var webhook = await FindWebhookAsync(org, project, webhookSlug);
                
                if (webhook == null)
                {
                    _logger.LogWarning(
                        "Webhook not found or disabled: {Owner}/{Project}/{Slug}",
                        org, project, webhookSlug);
                    return NotFound();
                }

                _logger.LogInformation(
                    "Found webhook {WebhookId} ({WebhookName}), backend type: {BackendType}, delivery mode: {DeliveryMode}",
                    webhook.Id, webhook.Name, webhook.BackendType, webhook.DeliveryMode);

                // Convert query parameters to JSON payload
                var queryParams = new Dictionary<string, string>();
                foreach (var key in Request.Query.Keys)
                {
                    queryParams[key] = Request.Query[key].ToString();
                }
                
                // Add timestamp if not already present
                if (!queryParams.ContainsKey("timestamp"))
                {
                    queryParams["timestamp"] = DateTime.UtcNow.ToString("o");
                }

                var serializedPayload = System.Text.Json.JsonSerializer.Serialize(queryParams);
                var webhookEvent = await ProcessWebhookEventAsync(webhook, serializedPayload);

                return CreatedAtAction(nameof(GetWebhookEvent), new { id = webhookEvent.Id }, WebhookEventDto.FromWebhookEvent(webhookEvent));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook GET submission");
                return BadRequest(ex.Message);
            }
        }

        // POST: api/wes/:org/:project/:webhookSlug
        [HttpPost("{org}/{project}/{webhookSlug}")]
        public async Task<ActionResult<WebhookEventDto>> PostWebhookEvent(String org, String project, String webhookSlug, [FromBody] object payload)
        {
            try
            {
                _logger.LogInformation(
                    "Received webhook POST request for {Owner}/{Project}/{Slug}",
                    org, project, webhookSlug);
                
                var webhook = await FindWebhookAsync(org, project, webhookSlug);
                
                if (webhook == null)
                {
                    _logger.LogWarning(
                        "Webhook not found or disabled: {Owner}/{Project}/{Slug}",
                        org, project, webhookSlug);
                    return NotFound();
                }

                _logger.LogInformation(
                    "Found webhook {WebhookId} ({WebhookName}), backend type: {BackendType}, delivery mode: {DeliveryMode}",
                    webhook.Id, webhook.Name, webhook.BackendType, webhook.DeliveryMode);

                var serializedPayload = System.Text.Json.JsonSerializer.Serialize(payload);
                var webhookEvent = await ProcessWebhookEventAsync(webhook, serializedPayload);

                return CreatedAtAction(nameof(PostWebhookEvent), new { id = webhookEvent.Id }, WebhookEventDto.FromWebhookEvent(webhookEvent));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook POST submission");
                return BadRequest(ex.Message);
            }
        }
    }
}
