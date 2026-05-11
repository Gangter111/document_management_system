# System Architecture

**Status**: Clean layered architecture | **Last Updated**: 2026-05-11

## Overview

The DocumentManagement system is a two-tier ASP.NET Core-based document management platform with a Windows desktop client and a centralized API server.

```
┌─────────────────────────────┐
│  WPF Desktop Clients        │
│  (DocumentManagement.Wpf)   │
└──────────────┬──────────────┘
               │ HTTP/HTTPS
               ▼
┌──────────────────────────────┐
│  ASP.NET Core API Server     │
│  (DocumentManagement.Api)    │
│  - Controllers (HTTP Handler)│
│  - Middleware (Security)     │
│  - Dependency Injection      │
└──────────────┬───────────────┘
               │
      ┌────────┼────────┐
      ▼        ▼        ▼
┌──────────┬──────────┬───────────────┐
│ Business │ Data     │ Infrastructure│
│ Logic    │ Access   │ Services      │
├──────────┼──────────┼───────────────┤
│Application   │  Infrastructure     │
│.Application  │  .Infrastructure    │
└──────────┬──────────┴───────────────┘
           │
           ▼
    ┌──────────────────┐
    │  Database        │
    │  (SQLite/SQL Srv)│
    │  File Storage    │
    └──────────────────┘
```

---

## Project Structure

| Project | Responsibility | Key Classes |
|---------|---|---|
| **DocumentManagement.Api** | HTTP endpoint, routing, middleware | Controllers, Program.cs, Health checks |
| **DocumentManagement.Application** | Business logic, use cases | Services (Document, Backup, User), Security |
| **DocumentManagement.Infrastructure** | Data persistence, external services | Repositories, Database, File storage |
| **DocumentManagement.Domain** | Domain models, entities, enums | Document, Attachment, User, Role entities |
| **DocumentManagement.Contracts** | API contracts, DTOs | Request/response objects |
| **DocumentManagement.Tests** | Integration tests | Controller tests, flow validation |
| **DocumentManagement.Wpf** | Desktop client UI | Windows forms, client logic |

---

## Layered Architecture

### Layer 1: API Layer (Controllers)
- **Responsibility**: HTTP request handling, routing, response formatting
- **Key Files**: `DocumentManagement.Api/Controllers/*`
- **Rules**:
  - Keep thin; no business logic
  - All routes except `/api/auth/login` require `[Authorize]`
  - Validate DTOs and reject invalid input with 400
  - Delegate to application services

### Layer 2: Application Layer (Business Logic)
- **Responsibility**: Use case orchestration, business rules
- **Key Files**: `DocumentManagement.Application/Services/*`
- **Rules**:
  - Independent of HTTP; could be called by other clients
  - Enforce authorization and role checks
  - Transaction and exception handling
  - Return domain objects or application models

### Layer 3: Infrastructure Layer (Persistence)
- **Responsibility**: Database access, file storage, external integrations
- **Key Files**: `DocumentManagement.Infrastructure/Repositories/*`, `Data/*`
- **Rules**:
  - Encapsulate SQL and file I/O
  - Provide repository pattern for data access
  - No business logic; just CRUD and queries
  - Support multiple database providers (SQLite, SQL Server)

### Layer 4: Domain Layer (Models)
- **Responsibility**: Core domain entities, value objects, enums
- **Key Files**: `DocumentManagement.Domain/Entities/*`, `Enums/*`
- **Rules**:
  - No external dependencies
  - Represent application domain concepts
  - Enforce domain invariants
  - No infrastructure code

### Layer 5: Contracts Layer (API Contracts)
- **Responsibility**: API request/response DTOs
- **Key Files**: `DocumentManagement.Contracts/*`
- **Rules**:
  - Define API surface
  - Never expose domain entities directly
  - Include validation attributes
  - Version if needed

---

## Data Flow Example: Create Document

```
1. Client sends POST /api/documents with DocumentCreateDto
                    ↓
2. DocumentsController.Create(DocumentCreateDto)
   - Validate DTO
   - Check authorization (not Staff)
   - Call service
                    ↓
3. DocumentService.CreateDocument(command)
   - Apply business rules
   - Call repositories
   - Log audit event
   - Return domain Document object
                    ↓
4. DocumentRepository.Add(document)
   - Execute SQL INSERT
   - Return created entity
                    ↓
5. Controller maps Document → DocumentReadDto
                    ↓
6. Return 201 Created with DocumentReadDto
```

---

## Authentication & Authorization

### JWT Token Flow

```
1. POST /api/auth/login (username, password)
   ↓
2. AuthService.Login()
   - Query Users by username
   - Verify password (hashed)
   - Query user roles
   ↓
3. Build JWT with claims:
   - sub (user ID)
   - http://schemas.microsoft.com/ws/2008/06/identity/claims/role
   - Department (custom)
   ↓
4. Return JWT in response
   ↓
5. Client stores JWT and includes in Authorization: Bearer header
   ↓
6. API validates JWT on each request
   - Verify signature
   - Check issuer/audience
   - Check expiration
   - Verify user still active
```

### Role-Based Access Control (RBAC)

See [RBAC Documentation](../security/RBAC.md) for complete permission matrix.

---

## Database Design

### Core Tables

| Table | Purpose |
|-------|---------|
| `documents` | Main document records |
| `document_attachments` | PDF and supporting files |
| `document_statuses` | Document workflow states |
| `document_categories` | Document classification |
| `document_history` | Audit trail (soft deletes) |
| `audit_logs` | System activity log |
| `Users` | User accounts |
| `Roles` | Application roles |

### Design Notes

- **No normalized user/role junction table**: Roles are stored as claims in JWT
- **Soft deletes via history**: Deleted documents move to history; SearchDocuments filters them out
- **Department scoping**: ProcessingDepartment field on documents enables multi-tenant scoping
- **Direct ADO.NET access**: Not using EF Core for runtime; using repositories with SQL

---

## Key Architectural Patterns

### Repository Pattern
- Abstraction over data access
- Single responsibility: CRUD operations
- Used for: Documents, Users, Audit logs

### Service Pattern
- Business logic orchestration
- Calls repositories and other services
- Handles exceptions and logging

### DTO Pattern
- Separate request/response contracts from domain
- Enables API versioning
- Protects internal model changes

### Dependency Injection
- `Program.cs` configures services
- Built-in .NET Core DI container
- Constructor injection in controllers and services

### Global Exception Handler
- Middleware intercepts all unhandled exceptions
- Returns generic JSON error response
- Logs full details for support

---

## Configuration

### Environment-Based

```
appsettings.json          # Development defaults
appsettings.Development.json   # Dev overrides
appsettings.Staging.template.json  # Staging template
appsettings.Production.template.json # Prod template
```

### Key Settings

- `Jwt:Secret` - JWT signing key (must be unique per environment)
- `Jwt:Issuer`, `Jwt:Audience` - JWT metadata
- `ConnectionStrings:DefaultConnection` - Database connection
- `Database:Provider` - "SQLite" or "SqlServer"
- `Backup:AllowApiRestore` - Whether API restore is enabled
- `Logging:LogLevel` - Min log severity

---

## Startup Flow

```
1. Program.cs: Main()
   ↓
2. WebApplicationBuilder configured:
   - Services registered (DI container)
   - Middleware added (pipeline)
   ↓
3. DatabaseMigrator runs
   - Creates/updates schema
   - Seeds initial data (users, roles)
   ↓
4. app.Run() starts Kestrel
   ↓
5. API listens on port 5033 (dev) or configured port
```

---

## Deployment Topology

### Development

```
Developer Machine
  └─ Visual Studio / VS Code
     └─ dotnet run
        └─ Kestrel on http://localhost:5033
           └─ SQLite in ./database/app.db
```

### Pilot / Staging

```
Staging Server (Windows)
  └─ IIS or Windows Service
     └─ DocumentManagement.Api.exe
        └─ Kestrel on http://127.0.0.1:5034
           └─ Reverse proxy (IIS) TLS
              └─ SQLite or SQL Server
```

### Production

```
Production Server (Windows Server)
  └─ IIS + Windows Service
     └─ DocumentManagement.Api.exe
        └─ Kestrel on internal port
           └─ IIS / Reverse Proxy (TLS)
              └─ SQL Server
                 └─ Network backup storage
```

---

## Monitoring & Observability

### Health Check
- `GET /health` - Database connectivity status

### Logging
- **Framework**: Serilog
- **Outputs**: Console (dev), Rolling file (prod)
- **Retention**: 30 rolling files
- **Levels**: Information (prod), Debug (dev)

### Request Logging
- Middleware logs all HTTP requests
- Global exception handler logs 5xx errors

---

## Security Architecture

See [Security Documentation](../security/SECURITY.md) for full details.

**Key Controls**:
- JWT validation on every authenticated request
- Role-based authorization in controllers
- Active-user middleware enforces deactivation
- Audit logging for destructive operations
- Upload validation (PDF MIME type, file signature)
- Backup/restore restricted (API restore default-disabled)

---

## Performance Considerations

### Identified Issues & Recommendations

| Issue | Impact | Mitigation |
|-------|--------|-----------|
| Dashboard loads all documents | High memory | Use SQL aggregation |
| Search unbounded page size | Large payloads | Implement pagination |
| Per-request user lookup | DB latency | Cache or token versioning |
| `SELECT *` in repositories | Over-fetching | Project columns needed |

See [Performance Review](../archive/GENERATED_REPORTS/PERFORMANCE_REVIEW.md) for full analysis.

---

## Future Enhancements

**Recommended**:
- Centralized authorization policies
- Token revocation/security stamp
- Full-text search
- Pagination cache

**Optional**:
- Docker containerization
- Kubernetes orchestration
- Distributed logging (ELK, Splunk)
- Distributed tracing (OpenTelemetry)

---

**Archive**: [ARCHITECTURE_REPORT.md](../archive/GENERATED_REPORTS/ARCHITECTURE_REPORT.md)

**Related**: [Security](../security/README.md) | [Deployment](CHECKLIST.md) | [Operations](../operations/OPERATIONAL_GUIDE.md)
