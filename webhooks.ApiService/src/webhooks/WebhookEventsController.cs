using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using webhooks.ApiService.src.models;
using System.Threading.Tasks;
using static webhooks.ApiService.src.models.WebhookEvent;

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

            return Ok(webhookEvents);
        }

        // GET: api/webhookevents/receive/{webhookId}
        [HttpGet("receive/{webhookId}")]
        public async Task<ActionResult<WebhookEvent>> ReceiveWebhookEvent(Guid webhookId)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Get the oldest webhook event that has not been received
                    var webhookEvent = await _context.WebhookEvents
                        .Where(we => we.WebhookId == webhookId && we.Status == WebhookEventStatus.New)
                        .OrderBy(we => we.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (webhookEvent == null)
                    {
                        return NotFound();
                    }

                    // Mark it as received
                    webhookEvent.Status = WebhookEventStatus.Received;
                    webhookEvent.UpdatedAt = DateTime.UtcNow;

                    // Save changes
                    await _context.SaveChangesAsync();

                    // Commit transaction
                    await transaction.CommitAsync();

                    return Ok(webhookEvent);
                }
                catch (Exception)
                {
                    // Rollback transaction if any error occurs
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }


        [HttpPut("return/{webhookEventId}")]
        public async Task<ActionResult<WebhookEvent>> ReturnWebhookEvent(Guid webhookEventId, [FromBody] WebhookEventWorkResponse wewr)
        {
            // update webhook event status to completed + status and add result text
            var webhookEvent = await _context.WebhookEvents.FirstOrDefaultAsync(we => we.Id == webhookEventId);
            if (webhookEvent == null)
            {
                return NotFound();

            }
            webhookEvent.Status = WebhookEventStatus.Processed;
            webhookEvent.SubStatus = wewr.Status;
            webhookEvent.StatusResultText = wewr.ResultText;
            webhookEvent.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(webhookEvent);
        }
    }

}
