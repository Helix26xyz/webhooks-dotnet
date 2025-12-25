using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace webhooks.SharedModels.security
{
    /// <summary>
    /// AES-256 encryption service for sensitive data (passwords, connection strings, API keys, etc.)
    /// Uses a shared secret key from configuration for multi-instance deployments.
    /// This service is application-wide and can be used to encrypt any sensitive string data.
    /// </summary>
    public class AesEncryptionService : IEncryptionService
    {
        private readonly byte[] _key;
        private readonly ILogger<AesEncryptionService> _logger;
        private const string EncryptionPrefix = "ENC:"; // Marker to identify encrypted values

        public AesEncryptionService(IConfiguration configuration, ILogger<AesEncryptionService> logger)
        {
            _logger = logger;
            
            // Get encryption key from configuration (env var or appsettings)
            // Priority: 1) appsettings.json, 2) environment variable
            var keyString = configuration["Encryption:SecretKey"] 
                ?? Environment.GetEnvironmentVariable("ENCRYPTION_SECRET_KEY")
                ?? Environment.GetEnvironmentVariable("WEBHOOK_ENCRYPTION_KEY") // Backward compatibility
                ?? throw new InvalidOperationException(
                    "Encryption key not found. Set 'Encryption:SecretKey' in appsettings.json or ENCRYPTION_SECRET_KEY environment variable");

            // Validate key length (must be 32 bytes for AES-256)
            if (keyString.Length < 32)
            {
                throw new InvalidOperationException(
                    $"Encryption key must be at least 32 characters. Current length: {keyString.Length}");
            }

            // Use first 32 bytes of the key for AES-256
            _key = Encoding.UTF8.GetBytes(keyString.Substring(0, 32));
            
            _logger.LogInformation("Encryption service initialized with AES-256");
        }

        public string Encrypt(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext))
            {
                return plaintext;
            }

            // Don't double-encrypt
            if (IsEncrypted(plaintext))
            {
                return plaintext;
            }

            try
            {
                using var aes = Aes.Create();
                aes.Key = _key;
                aes.GenerateIV(); // Generate random IV for each encryption

                using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream();
                
                // Write IV to the beginning of the stream (needed for decryption)
                ms.Write(aes.IV, 0, aes.IV.Length);
                
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs))
                {
                    sw.Write(plaintext);
                }

                var encrypted = ms.ToArray();
                return EncryptionPrefix + Convert.ToBase64String(encrypted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encrypting data");
                throw new InvalidOperationException("Encryption failed", ex);
            }
        }

        public string Decrypt(string ciphertext)
        {
            if (string.IsNullOrEmpty(ciphertext))
            {
                return ciphertext;
            }

            // If not encrypted, return as-is (for backward compatibility during migration)
            if (!IsEncrypted(ciphertext))
            {
                _logger.LogWarning("Attempting to decrypt non-encrypted value. This should only happen during migration.");
                return ciphertext;
            }

            try
            {
                // Remove encryption prefix
                var base64Data = ciphertext.Substring(EncryptionPrefix.Length);
                var fullCipher = Convert.FromBase64String(base64Data);

                using var aes = Aes.Create();
                aes.Key = _key;

                // Extract IV from the beginning of the cipher (first 16 bytes for AES)
                var iv = new byte[aes.IV.Length];
                var cipher = new byte[fullCipher.Length - iv.Length];
                
                Array.Copy(fullCipher, 0, iv, 0, iv.Length);
                Array.Copy(fullCipher, iv.Length, cipher, 0, cipher.Length);
                
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream(cipher);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);
                
                return sr.ReadToEnd();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting data");
                throw new InvalidOperationException("Decryption failed. Data may be corrupted or key may be incorrect.", ex);
            }
        }

        public bool IsEncrypted(string? value)
        {
            return value?.StartsWith(EncryptionPrefix) ?? false;
        }
    }
}
