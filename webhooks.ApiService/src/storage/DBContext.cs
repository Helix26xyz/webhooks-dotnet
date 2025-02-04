using Microsoft.EntityFrameworkCore;
using webhooks.ApiService.src.models;

namespace webhooks.ApiService.src
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Define your DbSets (tables) here
        public DbSet<Webhook> Webhooks { get; set; }
        public DbSet<WebhookEvent> WebhookEvents { get; set; }
        public DbSet<WebhookEventStatusEntity> WebhookEventStatuses { get; set; }
        public DbSet<WebhookEventSubStatusEntity> WebhookEventSubStatuses { get; set; }
    }
}
