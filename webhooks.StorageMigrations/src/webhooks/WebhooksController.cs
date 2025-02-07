using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using webhooks.SharedModels.models;
using System.Threading.Tasks;
using webhooks.SharedModels.storage;

namespace webhooks.StorageMigrations.src
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
        public async Task<ActionResult<IEnumerable<Webhook>>> GetWebhooks()
        {
            return await _context.Webhooks.ToListAsync();
        }

        // GET: api/Webhooks/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Webhook>> GetWebhook(Guid id)
        {
            var webhook = await _context.Webhooks.FindAsync(id);

            if (webhook == null)
            {
                return NotFound();
            }

            return webhook;
        }

        // POST: api/Webhooks
        [HttpPost]
        public async Task<ActionResult<Webhook>> PostWebhook(Webhook webhook)
        {
            try
            {
                _context.Webhooks.Add(webhook);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetWebhook), new { id = webhook.Id }, webhook);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
            }

        // PUT: api/Webhooks/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutWebhook(Guid id, Webhook webhook)
        {
            if (id != webhook.Id)
            {
                return BadRequest();
            }

            var existingEntity = await _context.Webhooks.FindAsync(id);
            if (existingEntity != null)
            {
                _context.Entry(existingEntity).State = EntityState.Detached;
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
