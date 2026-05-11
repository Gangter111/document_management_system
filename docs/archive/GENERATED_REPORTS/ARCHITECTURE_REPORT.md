# Architecture Report

Generated: 2026-05-11

## Scope

This report covers architecture only. No source code was modified.

Audited root solution:

- `D:\QuanLyVanBan - V2\DocumentManagement.sln`

The repository also contains a duplicated `document_management_system` tree. This report treats the root solution as authoritative because it is the workspace root and the solution referenced by the current task.

## Solution Structure

| Project | Responsibility | Architectural Notes |
| --- | --- | --- |
| `DocumentManagement.Api` | ASP.NET Core Web API host, controllers, middleware, JWT, health checks, startup wiring | Owns HTTP boundary and composition root. |
| `DocumentManagement.Application` | Application interfaces, application services, security helpers, application models | Contains core use-case logic for documents and attachments. |
| `DocumentManagement.Infrastructure` | SQL repositories, database migrators, file storage, auth implementation, PDF/OCR/report infrastructure | Runtime persistence is direct ADO.NET-style SQL, not EF Core. |
| `DocumentManagement.Domain` | Domain entities and enums | Shared domain model. |
| `DocumentManagement.Contracts` | Request/response DTOs | API and WPF client contract layer. |
| `DocumentManagement.Wpf` | Desktop client | Included in solution but outside API server architecture. |

## Architectural Style

The codebase follows a mostly clean layered architecture:

```text
WPF Client / HTTP Clients
        |
DocumentManagement.Api
        |
DocumentManagement.Contracts
        |
DocumentManagement.Application
        |
DocumentManagement.Domain
        |
DocumentManagement.Infrastructure
        |
SQLite / SQL Server / File Storage
```

Dependency direction is generally acceptable:

- API references Application, Infrastructure, Domain, and Contracts.
- Infrastructure implements Application interfaces.
- Application depends on Domain abstractions/models.
- Contracts are DTO-only.
- Domain does not depend on outer layers.

No project-level circular dependency was observed from the solution structure.

## Startup Flow

`DocumentManagement.Api/Program.cs` is the composition root.

Startup sequence:

1. Configure Windows Service hosting.
2. Configure Serilog console and rolling file logs.
3. Resolve database provider from `Database` configuration.
4. Create either SQLite or SQL Server connection factory.
5. Register controllers, data protection, health checks, Swagger, repositories, services, JWT, and current-user services.
6. Validate JWT secret length and reject placeholder secret in production.
7. Configure JWT bearer authentication.
8. Run database migration at application startup.
9. Build HTTP pipeline:
   - Serilog request logging
   - Global exception handler
   - Swagger only in development
   - Authentication
   - Custom active-user middleware
   - Authorization
   - `/health`
   - Controllers

## Dependency Injection Review

Observed registrations:

- Singleton:
  - `IDbConnectionFactory`
  - `IDatabaseDialect`
  - `IFileStorageService`
- Scoped:
  - repositories
  - `IDocumentService`
  - `IAuthService`
  - `IAttachmentService`
  - `IOcrService`
  - `JwtService`
  - `ICurrentUserService`

Assessment:

- Composition is centralized and readable.
- Database provider selection is cleanly abstracted through factory/dialect interfaces.
- Some implemented services are not wired in the current startup path, such as `BackupService`, `ExcelReportService`, `CachedDocumentService`, and `PermissionService`.
- Some controllers depend directly on repositories instead of application services, weakening the intended service boundary.

## Authentication Flow

Authentication path:

1. `POST /api/auth/login` is anonymous.
2. `AuthController` delegates credential verification to `IAuthService`.
3. `AuthService` reads active user and role data from `Users`/`Roles`.
4. BCrypt is used for password verification.
5. `JwtService` issues a JWT containing:
   - user id
   - username
   - role
   - full name
   - department
6. JWT bearer middleware validates issuer, audience, signing key, and lifetime.
7. A custom middleware checks the token user still exists and is active.
8. Controllers use `UserPrincipalExtensions` for manual role and department checks.

Architectural concern:

- Authorization policy is mostly embedded in controllers rather than ASP.NET Core authorization policies or application services.
- The active-user middleware directly queries the database from `Program.cs`, tying middleware to table structure.

## Database Access Architecture

Runtime persistence uses direct SQL through:

- `IDbConnectionFactory`
- `IDatabaseDialect`
- repository implementations
- SQLite and SQL Server migrators

Notable architecture details:

- The active code path is not EF Core.
- SQL dialect behavior is abstracted with `SqliteDialect` and `SqlServerDialect`.
- Repositories map database records to domain/application models manually.
- Database migration is performed during API startup.

Architectural concern:

- `DocumentManagement.Infrastructure/Data/AppDbContext.cs` exists with stale `DocumentRegistry.Api.*` namespace references and does not match the active persistence pattern. It is confusing as an architecture signal.

## Controller Boundary Review

Controllers that are relatively thin:

- `LookupsController`
- `AuditLogsController`

Controllers carrying significant business/application logic:

- `DocumentsController`
  - permission checks
  - department scope checks
  - DTO mapping
  - upload temp-file handling
  - document state transition orchestration
- `BackupController`
  - backup generation
  - restore orchestration
  - SQLite validation
  - filesystem operations
- `DashboardController`
  - full document aggregation logic
- `ReportsController`
  - full document aggregation logic
- `SystemController`
  - user-management orchestration directly against repository
- `CatalogController`
  - catalog write authorization and direct repository orchestration

Assessment:

- The project has clean-layer intent, but several controllers are doing application/service work.
- The largest boundary violation is `BackupController`; backup/restore behavior should be behind an application/infrastructure service boundary.
- `DashboardController` and `ReportsController` should delegate aggregation to service/repository queries.

## Service Boundary Review

Good service ownership:

- `DocumentService` owns document create/update/delete side effects, including history and audit creation.
- `AttachmentService` owns file storage plus attachment record creation.
- `AuthService` owns login and password hash verification.

Weak or bypassed service ownership:

- User management goes from `SystemController` directly to `IUserManagementRepository`.
- Catalog management goes from `CatalogController` directly to `ICatalogRepository`.
- Backup endpoints do not use the existing `IBackupService`/`BackupService`.
- Dashboard API uses `IDocumentService.GetAllAsync()` and aggregates in the controller despite an `IDashboardRepository` existing.

## Middleware Pipeline Assessment

Current middleware order is reasonable for a basic API:

1. Request logging
2. Exception handling
3. Development Swagger
4. Authentication
5. Active-user validation
6. Authorization
7. Endpoint mapping

Gaps at architecture level:

- No explicit HTTPS middleware in the pipeline.
- No explicit CORS policy.
- Active-user validation is inline middleware rather than a named middleware/service.
- Health endpoint is mapped directly and only checks database health.

## Code Smells and Architectural Risks

| Severity | Finding | Evidence | Impact |
| --- | --- | --- | --- |
| High | Duplicate source tree exists | `document_management_system/*` | Wrong-tree edits/deployments are likely during maintenance. |
| High | Backup logic is controller-owned | `BackupController.cs` is large and performs restore/filesystem/database validation | Hard to test and risky to evolve safely. |
| High | Dashboard/reports aggregate through full document loads | `DashboardController`, `ReportsController` | Performance and layering issue as data grows. |
| Medium | Fat document controller | `DocumentsController.cs` is the largest controller and owns permissions/mapping/upload orchestration | Business rules are hard to reuse and test. |
| Medium | Direct repository use from API for user/catalog flows | `SystemController`, `CatalogController` | Application layer is bypassed. |
| Medium | Manual authorization checks repeated in controllers | Multiple controllers | Inconsistent enforcement risk over time. |
| Medium | Stale EF `AppDbContext` remains | `Infrastructure/Data/AppDbContext.cs` | Confuses actual persistence architecture. |
| Medium | Startup runs migrations synchronously | `Program.cs`, migrator classes | Startup behavior is coupled to schema mutation. |
| Low | Manual DTO mapping repeated in controllers | Multiple controllers | Boilerplate and drift risk. |

## Architecture Recommendations

Minimal, safe direction:

1. Keep the existing project layering.
2. Treat `DocumentManagement.Api/Program.cs` as the only composition root.
3. Decide whether `document_management_system` is archival; exclude or remove it from active delivery once confirmed.
4. Move backup/restore orchestration behind `IBackupService` before expanding backup features.
5. Move dashboard/report aggregation into application services backed by SQL aggregate repository methods.
6. Introduce named authorization policies gradually, starting with admin-only and backup endpoints.
7. Add application services for user management and catalog management so controllers stop depending directly on repositories.
8. Quarantine or remove stale EF `AppDbContext` after confirming it is unused.
9. Keep future changes narrow and incremental; no aggressive architecture rewrite is needed.

## Overall Assessment

The architecture is recognizable and maintainable for a small internal system, with clean project boundaries and a clear runtime composition root. The main issue is not project layout; it is boundary erosion inside the API layer. Several controllers contain business rules, authorization decisions, and data orchestration that should live in application services.

Architectural readiness: **moderate**.

Recommended next step: stabilize boundaries around backup, dashboard/report aggregation, and authorization policies before broad pilot expansion.

