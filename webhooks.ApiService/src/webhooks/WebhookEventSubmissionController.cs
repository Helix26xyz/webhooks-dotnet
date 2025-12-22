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

        // GET: api/wes/:org/:project/:webhookSlug
        [HttpGet("{org}/{project}/{webhookSlug}")]
        public async Task<ActionResult<WebhookEvent>> GetWebhookEvent(String org, String project, String webhookSlug)
        {
            var webhook = await _context.Webhooks.FirstOrDefaultAsync(w => w.Slug == webhookSlug &&
            w.Owner == org &&
            w.Project == project &&
            w.Status != WebhookStatus.Disabled
            );
             if (webhook == null)
            {
                 return NotFound();
            }

            var webhookEvent = new WebhookEvent
            {
                WebhookId = webhook.Id,
                Status = WebhookEventStatus.New,
                SubStatus = WebhookEventSubStatus.Pending,
                Payload = string.Empty,
                Webhook = webhook

            };

            _context.WebhookEvents.Add(webhookEvent);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetWebhookEvent), new { id = webhookEvent.Id }, webhookEvent);
        }

        // POST: api/webhook/:webhookSlug
        [HttpPost("{org}/{project}/{webhookSlug}")]
        public async Task<ActionResult<WebhookEvent>> PostWebhookEvent(String org, String project, String webhookSlug, [FromBody] object payload)
        {
            try
            {
                var webhook = await _context.Webhooks.FirstOrDefaultAsync(w => w.Slug == webhookSlug &&
                    w.Owner == org &&
                    w.Project == project &&
                    w.Status != WebhookStatus.Disabled
                );
                
                if (webhook == null)
                {
                    return NotFound();
                }

                // Update last received timestamp
                webhook.LastReceivedAt = DateTime.UtcNow;

                var serializedPayload = System.Text.Json.JsonSerializer.Serialize(payload);

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
                    
                    // Decrypt BackendConfig for backend use only
                    var webhookForBackend = webhook.GetWebhookForBackend(_encryptionService);
                    
                    WebhookBackendResult? backendResult = null;
                    
                    if (webhook.DeliveryMode == WebhookDeliveryMode.Synchronous)
                    {
                        // Wait for backend to process (use decrypted webhook)
                        backendResult = await backend.SendAsync(webhookForBackend, serializedPayload, webhookEvent.Id);
                        
                        // Update webhook event with backend result
                        if (backendResult.Success)
                        {
                            webhookEvent.Status = WebhookEventStatus.Processed;
                            webhookEvent.SubStatus = WebhookEventSubStatus.Success;
                            webhookEvent.StatusResultText = backendResult.Message;
                        }
                        else
                        {
                            webhookEvent.Status = WebhookEventStatus.Processed;
                            webhookEvent.SubStatus = WebhookEventSubStatus.Failed;
                            webhookEvent.StatusResultText = backendResult.Message;
                        }
                        
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        // Fire-and-forget: Send to backend asynchronously
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                // Use decrypted webhook for backend
                                var result = await backend.SendAsync(webhookForBackend, serializedPayload, webhookEvent.Id);
                                
                                // Update event status in background
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
                    webhookEvent.Status = WebhookEventStatus.Processed;
                    webhookEvent.SubStatus = WebhookEventSubStatus.Failed;
                    webhookEvent.StatusResultText = $"Backend error: {backendEx.Message}";
                    await _context.SaveChangesAsync();
                }

                return CreatedAtAction(nameof(PostWebhookEvent), new { id = webhookEvent.Id }, webhookEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook submission");
                return BadRequest(ex.Message);
            }
        }
    }
}
