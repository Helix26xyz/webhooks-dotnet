using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using webhooks.SharedModels.models;
using System.Threading.Tasks;
using webhooks.SharedModels.storage;
using webhooks.SharedModels.security;
using webhooks.SharedModels.backends;

namespace webhooks.ApiService.src
{
    [Route("api/[controller]")]
    [ApiController]
    public class WebhooksController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IEncryptionService _encryptionService;
        private readonly IWebhookBackendFactory _backendFactory;

        public WebhooksController(AppDbContext context, IEncryptionService encryptionService, IWebhookBackendFactory backendFactory)
        {
            _context = context;
            _encryptionService = encryptionService;
            _backendFactory = backendFactory;
        }

        // GET: api/Webhooks
        [HttpGet]
        public async Task<ActionResult<IEnumerable<WebhookDto>>> GetWebhooks()
        {
            var webhooks = await _context.Webhooks.ToListAsync();
            return webhooks.Select(w => WebhookDto.FromWebhook(w)).ToList();
        }

        // GET: api/Webhooks/5
        [HttpGet("{id}")]
        public async Task<ActionResult<WebhookDto>> GetWebhook(Guid id)
        {
            var webhook = await _context.Webhooks.FindAsync(id);

            if (webhook == null)
            {
                return NotFound();
            }

            return WebhookDto.FromWebhook(webhook);
        }

        // POST: api/Webhooks
        [HttpPost]
        public async Task<ActionResult<WebhookDto>> PostWebhook(Webhook webhook)
        {
            try
            {
                // Encrypt BackendConfig before saving
                if (!string.IsNullOrEmpty(webhook.BackendConfig))
                {
                    webhook.SetBackendConfig(webhook.BackendConfig, _encryptionService);
                }

                _context.Webhooks.Add(webhook);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetWebhook), new { id = webhook.Id }, WebhookDto.FromWebhook(webhook));
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
            if (existingEntity == null)
            {
                return NotFound();
            }

            // Update fields
            existingEntity.Name = webhook.Name;
            existingEntity.Slug = webhook.Slug;
            existingEntity.Url = webhook.Url;
            existingEntity.CreatedBy = webhook.CreatedBy;
            existingEntity.Owner = webhook.Owner;
            existingEntity.Project = webhook.Project;
            existingEntity.Status = webhook.Status;
            existingEntity.BackendType = webhook.BackendType;
            existingEntity.DeliveryMode = webhook.DeliveryMode;

            // Only update BackendConfig if a new value is provided (allows updating config)
            // User must provide the full config, not partial updates
            if (!string.IsNullOrEmpty(webhook.BackendConfig))
            {
                existingEntity.SetBackendConfig(webhook.BackendConfig, _encryptionService);
            }

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

        // POST: api/Webhooks/test-connection
        [HttpPost("test-connection")]
        public async Task<ActionResult<WebhookBackendResult>> TestConnection(Webhook webhook)
        {
            try
            {
                // Encrypt the config before testing (mimics what would be saved)
                if (!string.IsNullOrEmpty(webhook.BackendConfig) && !_encryptionService.IsEncrypted(webhook.BackendConfig))
                {
                    webhook.BackendConfig = _encryptionService.Encrypt(webhook.BackendConfig);
                }

                // Get the appropriate backend
                var backend = _backendFactory.GetBackend(webhook.BackendType);

                // Decrypt BackendConfig for backend use only
                var webhookForBackend = webhook.GetWebhookForBackend(_encryptionService);

                // Test the connection using decrypted config
                var success = await backend.TestConnectionAsync(webhookForBackend);

                return Ok(new WebhookBackendResult
                {
                    Success = success,
                    Message = success ? "Connection test successful" : "Connection test failed"
                });
            }
            catch (Exception ex)
            {
                return Ok(new WebhookBackendResult
                {
                    Success = false,
                    Message = $"Test failed: {ex.Message}"
                });
            }
        }

        private bool WebhookExists(Guid id)
        {
            return _context.Webhooks.Any(e => e.Id == id);
        }
    }
}
