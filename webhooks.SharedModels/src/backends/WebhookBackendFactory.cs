using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using webhooks.SharedModels.models;

namespace webhooks.SharedModels.backends
{
    /// <summary>
    /// Factory for creating webhook backend instances based on webhook configuration
    /// </summary>
    public interface IWebhookBackendFactory
    {
        /// <summary>
        /// Get backend implementation for a webhook
        /// </summary>
        IWebhookBackend GetBackend(WebhookBackendType backendType);
    }

    public class WebhookBackendFactory : IWebhookBackendFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WebhookBackendFactory> _logger;
        private readonly Dictionary<WebhookBackendType, IWebhookBackend> _backendCache;

        public WebhookBackendFactory(IServiceProvider serviceProvider, ILogger<WebhookBackendFactory> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _backendCache = new Dictionary<WebhookBackendType, IWebhookBackend>();
        }

        public IWebhookBackend GetBackend(WebhookBackendType backendType)
        {
            // Check cache first
            if (_backendCache.TryGetValue(backendType, out var cachedBackend))
            {
                return cachedBackend;
            }

            // Get all registered backends
            var backends = _serviceProvider.GetServices<IWebhookBackend>();
            
            var backend = backends.FirstOrDefault(b => b.BackendType == backendType);
            
            if (backend == null)
            {
                _logger.LogWarning("No backend implementation found for type {BackendType}, falling back to Database", backendType);
                backend = backends.FirstOrDefault(b => b.BackendType == WebhookBackendType.Database);
                
                if (backend == null)
                {
                    throw new InvalidOperationException($"No backend implementation found for type {backendType} and no Database fallback available");
                }
            }

            // Cache for future use
            _backendCache[backendType] = backend;
            
            return backend;
        }
    }
}
