# Application-Wide Encryption Configuration

## Overview

The application uses **AES-256 encryption** for protecting sensitive data at rest, including:
- Backend connection strings (`Webhook.BackendConfig`)
- Database passwords
- API keys
- OAuth secrets
- Any other sensitive string data

This is a **shared, application-wide encryption service** that uses a single encryption key for all encrypted fields.

## Environment Variable (Recommended for Production)

Set the encryption key as an environment variable:

```bash
export ENCRYPTION_SECRET_KEY="your-32-character-minimum-encryption-key-here"
```

**Legacy/Backward Compatibility:**
```bash
export WEBHOOK_ENCRYPTION_KEY="your-32-character-minimum-encryption-key-here"
```

For Kubernetes deployments:

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: app-encryption-key
type: Opaque
stringData:
  ENCRYPTION_SECRET_KEY: "your-32-character-minimum-encryption-key-here"
```

Then reference in deployment:

```yaml
spec:
  containers:
  - name: webhook-api
    env:
    - name: ENCRYPTION_SECRET_KEY
      valueFrom:
        secretKeyRef:
          name: app-encryption-key
          key: ENCRYPTION_SECRET_KEY
```

## appsettings.json (Development Only)

For local development, add to `appsettings.Development.json`:

```json
{
  "Encryption": {
    "SecretKey": "development-key-min-32-chars-123"
  }
}
```

⚠️ **IMPORTANT**: 
- **NEVER commit encryption keys to source control**
- Key must be **minimum 32 characters** for AES-256
- Use the **same key across all instances** in multi-instance deployments
- Store in **secure secret management** (Azure Key Vault, AWS Secrets Manager, etc.)

## Key Generation

Generate a secure key:

```bash
# Linux/Mac
openssl rand -base64 32

# PowerShell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
```

## Priority Order

The encryption service checks for keys in this order:
1. `Encryption:SecretKey` in appsettings.json
2. `ENCRYPTION_SECRET_KEY` environment variable
3. `WEBHOOK_ENCRYPTION_KEY` environment variable (backward compatibility)

If none are found, the application will fail to start.

## Usage in Code

### For Any Model/Field

Use the generic `EncryptionHelper` class:

```csharp
using webhooks.SharedModels.models;
using webhooks.SharedModels.security;

// Encrypt a sensitive field
public class MyModel
{
    public string? ApiKey { get; set; }
    
    public void SetApiKey(string? apiKey, IEncryptionService encryptionService)
    {
        ApiKey = EncryptionHelper.EncryptValue(apiKey, encryptionService);
    }
    
    public string? GetApiKey(IEncryptionService encryptionService)
    {
        return EncryptionHelper.DecryptValue(ApiKey, encryptionService);
    }
}
```

### For Webhook Backend Config (Current Implementation)

```csharp
// Use specific extension methods
webhook.SetBackendConfig(config, encryptionService);
var decrypted = webhook.GetBackendConfig(encryptionService);
```

### Direct Usage

```csharp
// Inject IEncryptionService in your service/controller
public class MyService
{
    private readonly IEncryptionService _encryptionService;
    
    public MyService(IEncryptionService encryptionService)
    {
        _encryptionService = encryptionService;
    }
    
    public async Task SaveSecretAsync(string secret)
    {
        var encrypted = _encryptionService.Encrypt(secret);
        await _db.SaveAsync(encrypted);
    }
    
    public async Task<string> GetSecretAsync()
    {
        var encrypted = await _db.LoadAsync();
        return _encryptionService.Decrypt(encrypted);
    }
}
```
