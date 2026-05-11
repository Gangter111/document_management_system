# PROJECT: Document Management System

## STACK
- ASP.NET Core Web API
- Clean layered architecture
- SQL Server
- JWT Authentication

---

# ARCHITECTURE RULES

## Layer Responsibilities

### Controllers
- Must stay thin
- No business logic
- Only orchestration and validation

### Services
- Contain business logic
- Must not depend on Controllers

### Repositories / Data
- Responsible for persistence only
- Avoid leaking EF entities outside

### DTOs
- Required for API requests/responses
- Never expose EF entities directly

---

# SECURITY RULES

- Validate all uploads
- Restrict file extensions
- Restrict MIME types
- Never trust client input
- Protect backup endpoints
- Audit all destructive actions
- Never expose internal exceptions
- JWT validation required everywhere
- Role checks mandatory for admin APIs

---

# DATABASE RULES

- Avoid N+1 queries
- Prefer async queries
- Use AsNoTracking for readonly operations
- Pagination required for large datasets
- Avoid loading full tables into memory

---

# PERFORMANCE RULES

- Avoid blocking calls
- Avoid synchronous IO
- Avoid unnecessary ToList()
- Avoid large payload responses
- Stream large file downloads

---

# DEPLOYMENT RULES

- No secrets in appsettings.json
- Environment separation required
- Production logging mandatory
- Health checks required
- Swagger must be restricted in production

---

# TESTING RULES

Before finishing any task:
1. Build solution
2. Run tests
3. Check warnings
4. Validate startup

---

# DEFINITION OF DONE

A task is NOT DONE unless:
- solution builds successfully
- no critical warnings
- no startup failures
- affected tests pass
- markdown report generated

---

# ESCALATION RULES

Before major refactoring:
1. Explain root cause
2. Explain proposed change
3. Explain impact
4. Then modify code

Never aggressively rewrite architecture.
Prefer minimal safe changes.