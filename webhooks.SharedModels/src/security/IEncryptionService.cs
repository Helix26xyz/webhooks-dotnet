namespace webhooks.SharedModels.security
{
    /// <summary>
    /// Service for encrypting and decrypting sensitive data (connection strings, passwords, API keys, etc.)
    /// This service can be used application-wide for any sensitive data that needs encryption at rest.
    /// </summary>
    public interface IEncryptionService
    {
        /// <summary>
        /// Encrypt plaintext data
        /// </summary>
        /// <param name="plaintext">The plaintext string to encrypt</param>
        /// <returns>Encrypted string with ENC: prefix</returns>
        string Encrypt(string plaintext);

        /// <summary>
        /// Decrypt encrypted data
        /// </summary>
        /// <param name="ciphertext">The encrypted string (should start with ENC: prefix)</param>
        /// <returns>Decrypted plaintext string</returns>
        string Decrypt(string ciphertext);

        /// <summary>
        /// Check if a string is encrypted (starts with encryption marker)
        /// </summary>
        /// <param name="value">The string to check</param>
        /// <returns>True if the string is encrypted, false otherwise</returns>
        bool IsEncrypted(string? value);
    }
}
