using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using webhooks.ApiService.src.models;
using System.Threading.Tasks;

namespace webhooks.ApiService.src
{
    [Route("api/webhook")]
    [ApiController]
    public class WebhookEventsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WebhookEventsController(AppDbContext context)
        {
            _context = context;
        }
        // GET: api/webhook/:webhookslug
        [HttpGet("{webhookslug}")]
        public async Task<ActionResult<WebhookEvent>> GetWebhookEvent(String webhookslug)
        {
            var webhook = await _context.Webhooks.FirstOrDefaultAsync(w => w.Slug == webhookslug && w.Status != WebhookStatus.Disabled);
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

        // POST: api/webhook/:webhookslug
        [HttpPost("{webhookslug}")]
        public async Task<ActionResult<WebhookEvent>> PostWebhookEvent(String webhookslug, [FromBody] string payload)
        {
            var webhook = await _context.Webhooks.FirstOrDefaultAsync(w => w.Slug == webhookslug && w.Status != WebhookStatus.Disabled);
            if (webhook == null)
            {
                return NotFound();
            }


            var webhookEvent = new WebhookEvent
            {
                Payload = payload,
                Status = WebhookEventStatus.New,
                SubStatus = WebhookEventSubStatus.Pending,
                Webhook = webhook
            };

            _context.WebhookEvents.Add(webhookEvent);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetWebhookEvent), new { id = webhookEvent.Id }, webhookEvent);
        }

        //// GET: api/webhookevents/:id
        //[HttpGet("{id}")]
        //public async Task<ActionResult<WebhookEvent>> GetWebhookEvent(Guid id)
        //{
        //    var webhookEvent = await _context.WebhookEvents.FindAsync(id);

        //    if (webhookEvent == null)
        //    {
        //        return NotFound();
        //    }

        //    return Ok(webhookEvent);
        //}
    }
}
