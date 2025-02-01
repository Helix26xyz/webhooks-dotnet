using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using webhooks.ApiService.src.models;
using System.Threading.Tasks;

namespace webhooks.ApiService.src
{
    // triggering a webhook
    [Route("api/webhook")]
    [ApiController]
    public class WebhookEventsSubmissionController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WebhookEventsSubmissionController(AppDbContext context)
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
    }

    // CRUD on a specific WebhookEvent
    [Route("api/webhookevents")]
    [ApiController]
    public class WebhookEventsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WebhookEventsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/webhookevents
        [HttpGet()]
        public async Task<ActionResult<IEnumerable<WebhookEvent>>> GetWebhookEvents(Guid id)
        {
            var webhookevents = await _context.WebhookEvents.ToListAsync();
            return webhookevents;
        }

        // GET: api/webhookevents/:id
        [HttpGet("{id}")]
        public async Task<ActionResult<WebhookEvent>> GetWebhookEvent(Guid id)
        {
            var webhookevent = await _context.WebhookEvents.FirstOrDefaultAsync(w => w.Id == id);
            if (webhookevent == null)
            {
                return NotFound();
            }
            return webhookevent;
        }
        // GET: api/webhookevents/bywebhook/{webhookId}
        [HttpGet("bywebhook/{webhookId}")]
        public async Task<ActionResult<IEnumerable<WebhookEvent>>> GetWebhookEventsByWebhookId(Guid webhookId)
        {
            var webhookEvents = await _context.WebhookEvents
                .Where(we => we.WebhookId == webhookId)
                .ToListAsync();

            if (webhookEvents == null || !webhookEvents.Any())
            {
                return NotFound();
            }

            return Ok(webhookEvents);
        }

    }

}
