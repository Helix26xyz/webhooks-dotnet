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
