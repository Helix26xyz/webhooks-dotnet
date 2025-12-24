# Webhooks .NET Aspire Project

## Architecture Overview

This is a .NET 9 Aspire-orchestrated microservices application for managing webhooks via REST API. **Note:** This is currently a webhook management API - there's no webhook delivery/retry mechanism yet. External consumers interact directly with the API endpoints.

Key components:

- **webhooks.AppHost** - Aspire orchestrator that configures service discovery, health checks, and dependencies
- **webhooks.ApiService** - REST API backend with EF Core + SQL Server, exposes `/api/webhooks`, `/api/webhookevents`, `/api/webhooksubmissions`
- **webhooks.Web** - Blazor Server frontend with Redis output caching (standard framework integration), consumes ApiService via service discovery
- **webhooks.StorageMigrations** - Database migration runner that executes SQL scripts from `/migrations` folder and exits (runs before ApiService)
- **webhooks.SharedModels** - Shared DTOs, EF models, and HTTP clients used across projects
- **webhooks.DemoClient** - Example webhook consumer (commented out in AppHost)
- **webhooks.ServiceDefaults** - Common Aspire setup: OpenTelemetry, health checks, service discovery, resilience

## Webhook Backend System

### Backend Abstraction Pattern
The webhook system supports **pluggable backends** via `IWebhookBackend` interface:
- **DatabaseBackend** - Stores events in `WebhookSubmissions` table (default fallback)
- **KafkaBackend** - Sends events to Kafka topics with metadata headers

Backend configuration is stored in `Webhook.BackendConfig` (encrypted JSON using AES-256 with key from user secrets).

### Webhook Submission Endpoint
Pattern: `/api/wes/{org}/{project}/{slug}` (shorthand for webhook event submission)
- Supports both GET (query params) and POST (JSON body)
- Looks up webhook by unique `(Owner, Project, Slug)` combination
- Routes to configured backend (Kafka or Database)
- Returns `WebhookEventDto` with submission details
- **Constraint**: `(Owner, Project, Slug)` is unique per DB index `IX_Webhooks_Owner_Project_Slug`

### Kafka Integration
Configuration for remote Kafka servers:
```csharp
// Required timeouts for remote Kafka connectivity
MessageTimeoutMs = 30000,      // 30s for message delivery
RequestTimeoutMs = 15000,      // 15s for metadata requests
SocketTimeoutMs = 15000        // 15s for socket operations
```

Backend config format (encrypted in DB):
```json
{
  "BootstrapServers": "10.10.100.93:9092",
  "Topic": "test-topic"
}
```

Kafka messages include headers: `webhook-id`, `webhook-name`, `webhook-owner`, `webhook-project`, `webhook-slug`, `webhook-url`.

### Response DTO Pattern
Controllers return **simplified DTOs** excluding sensitive/internal data:
- `WebhookEventDto` - Omits `backendConfig`, `statusResultText`, `payload` 
- `WebhookSubmissionInfo` - Minimal webhook metadata (id, name, slug, url, owner, project)
- Enums serialize as **strings by default** (e.g., `"Pending"` not `0`)
- Optional integer enum format via `X-ENUMS-INT: 1` header

### Encryption Service
Backend configuration is encrypted using:
- Algorithm: AES-256-CBC
- Key storage: User secrets (`Encryption:Key`)
- Development key: `dev-local-demo-key-32chars-min!!` (32+ chars required)
- Used for `Webhook.BackendConfig` JSON before persisting to DB

## Critical Workflows

### Running the Application
```bash
# Full setup (first time only)
make setup_dev

# Run via Aspire (launches dashboard + all services)
aspire run
# Or: dotnet run --project webhooks.AppHost

# Individual service debugging - Aspire handles service discovery automatically
# ApiService will be at https+http://apiservice internally
```

### Testing
```bash
make test                 # Run unit + E2E tests
make test-coverage        # Generate coverage with reportgenerator
```

### Database Migrations
- SQL migrations live in `webhooks.StorageMigrations/migrations/*.sql`
- Migration service runs on startup (AppHost uses `.WaitFor(db)` to enforce order)
- Uses custom `DatabaseMigrationService` that tracks applied migrations in `__MigrationHistory` table

### Kubernetes Deployment
Uses **aspirate** tool to generate k8s manifests from Aspire configuration:
```bash
dotnet tool install --global aspirate --version 9.1.0
# Output generated in webhooks.AppHost/aspirate-output/
```
Config in [webhooks.AppHost/aspirate.json](webhooks.AppHost/aspirate.json) specifies `ghcr.io/helix26xyz` registry.

**Database Persistence**: SQL Server uses a StatefulSet with persistent volumes (10Gi default) to retain data across pod recreations. Volume is mounted at `/var/opt/mssql`. See [webhooks.AppHost/aspirate-output/DefaultConnection/README.md](webhooks.AppHost/aspirate-output/DefaultConnection/README.md) for details.

**Migration Jobs**: Storage migrations run as Kubernetes Jobs with hash-based naming to allow Flux reconciliation when image tags change. Jobs auto-cleanup after 5 minutes via `ttlSecondsAfterFinished: 300`. See [webhooks.AppHost/aspirate-output/storageMigrations/README.md](webhooks.AppHost/aspirate-output/storageMigrations/README.md) for details.

## Project-Specific Conventions

### Service Discovery
- Use `https+http://apiservice` URLs (not localhost) - Aspire resolves to correct endpoint
- Example in [webhooks.Web/Program.cs](webhooks.Web/Program.cs#L19):
  ```csharp
  client.BaseAddress = new("https+http://apiservice");
  ```

### Controllers and Models
- Controllers in `*.ApiService/src/webhooks/` follow REST conventions
- **No authentication/authorization implemented yet** - planned for future
- Shared models in `webhooks.SharedModels/src/`:
  - `/models` - EF entities (e.g., `Webhook`, `WebhookEvent`)
  - `/clients` - HTTP client wrappers (e.g., `WebhookApiClient`)
  - `/storage` - EF `AppDbContext` and repository patterns
- `Webhook` entity has `Status` enum (Enabled/Disabled/Suspended) and `Owner`/`Project` fields designed for multi-tenancy (Owner = tenant with one or more Projects)
- **Uniqueness constraint**: DB index `IX_Webhooks_Owner_Project_Slug` enforces unique (Owner, Project, Slug); API returns HTTP 409 Conflict on duplicate creation attempts

### Code Quality Patterns
- **Extract common logic**: When GET/POST endpoints share processing logic, extract to private methods (e.g., `ProcessWebhookEventAsync()`, `FindWebhookAsync()`)
- **Avoid debug logging in production**: Remove `Console.WriteLine` and verbose Kafka `Debug` settings before commit
- **DTO hygiene**: Response DTOs should exclude sensitive data (`backendConfig`, `statusResultText`) and internal fields (`payload`) that aren't needed by consumers

### Testing Patterns
- E2E tests use `InMemoryDatabase` with unique GUID names per test: `UseInMemoryDatabase($"TestDatabase_{Guid.NewGuid()}")`
- See [webhooks.Tests/WebhookE2ETests.cs](webhooks.Tests/WebhookE2ETests.cs#L27-L30)
- Tests seed data in constructor, test full controller CRUD workflows

### Aspire Dependency Chain
From [webhooks.AppHost/Program.cs](webhooks.AppHost/Program.cs):
```csharp
sql → db → storageMigrations (runs migrations) → apiService → webfrontend
                                                           ↘ cache (Redis)
```
Use `.WaitFor()` to ensure proper startup order.

### OpenAPI/Swagger
- ApiService exposes Swagger UI at root (`/`) in development mode
- OpenAPI spec at `/openapi/v1.json`

## Docker and Compose

- Docker Compose setup in [docker-compose.yml](docker-compose.yml) runs mssql + web + apiservice + storagemigrations
- SQL Server credentials: `sa` / `YourStrong!Passw0rd`
- Health checks configured for mssql using sqlcmd
- Images tagged as `helix26xyz/webhooks-dotnet/{service}:latest`

## Key Files
- [Makefile](Makefile) - Build/test/run commands (targets: `setup_dev`, `run`, `test`, `test-coverage`)
- [Directory.Build.props](Directory.Build.props) - Shared MSBuild properties for solution
- [webhooks.sln](webhooks.sln) - Solution file with 8 projects
