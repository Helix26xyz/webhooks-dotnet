using webhooks.SharedModels.security;

namespace webhooks.SharedModels.models
{
    /// <summary>
    /// Extension methods for Webhook to handle encrypted BackendConfig
    /// </summary>
    public static class WebhookEncryptionExtensions
    {
        /// <summary>
        /// Set backend config with automatic encryption
        /// </summary>
        public static void SetBackendConfig(this Webhook webhook, string? config, IEncryptionService encryptionService)
        {
            if (string.IsNullOrEmpty(config))
            {
                webhook.BackendConfig = null;
                return;
            }

            // Encrypt the config before storing
            webhook.BackendConfig = encryptionService.Encrypt(config);
        }

        /// <summary>
        /// Get decrypted backend config
        /// </summary>
        public static string? GetBackendConfig(this Webhook webhook, IEncryptionService encryptionService)
        {
            if (string.IsNullOrEmpty(webhook.BackendConfig))
            {
                return null;
            }

            // Decrypt the config when reading
            return encryptionService.Decrypt(webhook.BackendConfig);
        }

        /// <summary>
        /// Get webhook for backend use (decrypts BackendConfig in a temporary copy)
        /// </summary>
        public static Webhook GetWebhookForBackend(this Webhook webhook, IEncryptionService encryptionService)
        {
            return new Webhook
            {
                Id = webhook.Id,
                Name = webhook.Name,
                Slug = webhook.Slug,
                Url = webhook.Url,
                CreatedAt = webhook.CreatedAt,
                CreatedBy = webhook.CreatedBy,
                Owner = webhook.Owner,
                Project = webhook.Project,
                Status = webhook.Status,
                BackendType = webhook.BackendType,
                BackendConfig = webhook.GetBackendConfig(encryptionService), // Decrypted for backend use
                DeliveryMode = webhook.DeliveryMode,
                LastReceivedAt = webhook.LastReceivedAt
            };
        }
    }
}
