using webhooks.SharedModels.security;

namespace webhooks.SharedModels.models
{
    /// <summary>
    /// Generic helper class for encrypting/decrypting sensitive fields in any model
    /// </summary>
    public static class EncryptionHelper
    {
        /// <summary>
        /// Encrypt a value if it's not already encrypted
        /// </summary>
        /// <param name="value">The value to encrypt</param>
        /// <param name="encryptionService">The encryption service</param>
        /// <returns>Encrypted value or null if input is null/empty</returns>
        public static string? EncryptValue(string? value, IEncryptionService encryptionService)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            // Don't double-encrypt
            if (encryptionService.IsEncrypted(value))
            {
                return value;
            }

            return encryptionService.Encrypt(value);
        }

        /// <summary>
        /// Decrypt a value if it's encrypted
        /// </summary>
        /// <param name="value">The value to decrypt</param>
        /// <param name="encryptionService">The encryption service</param>
        /// <returns>Decrypted value or original value if not encrypted</returns>
        public static string? DecryptValue(string? value, IEncryptionService encryptionService)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            // Only decrypt if actually encrypted
            if (!encryptionService.IsEncrypted(value))
            {
                return value;
            }

            return encryptionService.Decrypt(value);
        }

        /// <summary>
        /// Check if a value is encrypted
        /// </summary>
        /// <param name="value">The value to check</param>
        /// <param name="encryptionService">The encryption service</param>
        /// <returns>True if the value is encrypted</returns>
        public static bool IsEncrypted(string? value, IEncryptionService encryptionService)
        {
            return encryptionService.IsEncrypted(value);
        }
    }
}
