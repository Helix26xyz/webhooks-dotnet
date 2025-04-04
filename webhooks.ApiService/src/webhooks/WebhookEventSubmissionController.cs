using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using webhooks.SharedModels.models;
using System.Threading.Tasks;
using webhooks.SharedModels.storage;

namespace webhooks.ApiService.src
{
    [Route("api/wes")]
    [ApiController]
    public class WebhookEventsSubmissionController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WebhookEventsSubmissionController(AppDbContext context)
        {
            _context = context;
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
            try{

            
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
                Payload = System.Text.Json.JsonSerializer.Serialize(payload),
                Status = WebhookEventStatus.New,
                SubStatus = WebhookEventSubStatus.Pending,
                Webhook = webhook
            };

            _context.WebhookEvents.Add(webhookEvent);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(PostWebhookEvent), new { id = webhookEvent.Id }, webhookEvent);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
