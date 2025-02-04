using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using webhooks.ApiService.src.models;
using System.Threading.Tasks;

namespace webhooks.ApiService.src
{
    // CRUD on a specific WebhookEvent
    [Route("api/[controller]")]
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

            //if (webhookEvents == null || !webhookEvents.Any())
            //{
            //    return NotFound();
            //}

            return Ok(webhookEvents);
        }

    }

}
