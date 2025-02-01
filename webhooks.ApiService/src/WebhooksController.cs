using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using webhooks.ApiService.src.models;
using System.Threading.Tasks;

namespace webhooks.ApiService.src
{
    [Route("api/[controller]")]
    [ApiController]
    public class WebhooksController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WebhooksController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Webhooks
        [HttpGet]
        public async Task<ActionResult<IEnumerable<IWebhook>>> GetWebhooks()
        {
            return await _context.Webhooks.ToListAsync();
        }

        // GET: api/Webhooks/5
        [HttpGet("{id}")]
        public async Task<ActionResult<IWebhook>> GetWebhook(Guid id)
        {
            var webhook = await _context.Webhooks.FindAsync(id);

            if (webhook == null)
            {
                return NotFound();
            }

            return Ok(webhook);
        }

        // POST: api/Webhooks
        [HttpPost]
        public async Task<ActionResult<IWebhook>> PostWebhook(IWebhook webhook)
        {
            _context.Webhooks.Add((Webhook)webhook);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetWebhook), new { id = webhook.Id }, webhook);
        }

        // PUT: api/Webhooks/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutWebhook(Guid id, IWebhook webhook)
        {
            if (id != webhook.Id)
            {
                return BadRequest();
            }

            _context.Entry(webhook).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!WebhookExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // DELETE: api/Webhooks/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteWebhook(Guid id)
        {
            var webhook = await _context.Webhooks.FindAsync(id);
            if (webhook == null)
            {
                return NotFound();
            }

            _context.Webhooks.Remove(webhook);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool WebhookExists(Guid id)
        {
            return _context.Webhooks.Any(e => e.Id == id);
        }
    }
}
