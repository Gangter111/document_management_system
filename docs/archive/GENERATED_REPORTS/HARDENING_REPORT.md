# Hardening Report

Generated: 2026-05-11

## Scope

Hardening-only changes were applied. No new features were added.

Focus areas:

- security hardening
- authorization correctness
- upload protection
- backup/restore safety
- production configuration
- operational safety
- audit integrity
- pagination and endpoint protection

## Root Causes Addressed

1. Production JWT transport settings were permissive because bearer HTTPS metadata validation was disabled globally.
2. PDF extraction trusted filename extension and did not validate MIME type or file signature before parser handoff.
3. SQLite restore accepted files based on extension and minimal table checks.
4. API restore was available unless blocked operationally; Production needed a default-deny switch.
5. Backup operations had limited audit visibility.
6. Document read endpoints allowed every authenticated user to read all active documents.
7. Search/list endpoints allowed large unbounded responses.
8. Self-service password changes accepted a valid JWT without re-verifying the current password.
9. Anonymous login had no application-level request throttle.
10. User and catalog administration mutations were not written to the audit trail.

## Changes Applied

### JWT / Production Transport

File:

- `DocumentManagement.Api/Program.cs`

Change:

- `JwtBearerOptions.RequireHttpsMetadata` is now enabled in Production and remains relaxed outside Production.

Risk reduced:

- Prevents Production deployments from silently accepting non-HTTPS token metadata behavior.

### Upload Protection

File:

- `DocumentManagement.Api/Controllers/DocumentsController.cs`

Changes:

- PDF extraction now checks allowed content types:
  - `application/pdf`
  - `application/octet-stream`
- PDF extraction now validates the `%PDF-` file signature before writing to temp and invoking OCR/parsing.

Risk reduced:

- Reduces parser exposure to renamed non-PDF payloads.

Regression risk:

- Some clients may upload PDFs with unusual content types and now receive `400 BadRequest`.

### Backup / Restore Safety

Files:

- `DocumentManagement.Api/Controllers/BackupController.cs`
- `DocumentManagement.Api/appsettings.json`
- `DocumentManagement.Api/appsettings.Production.template.json`

Changes:

- Added `Backup:AllowApiRestore`.
- Production template defaults API restore to disabled.
- Development/default config allows restore to preserve current local/test behavior.
- Restore validates SQLite file header before opening the uploaded database.
- Fixed malformed brace structure around restore validation while preserving behavior.

Risk reduced:

- Production restore is now default-deny unless explicitly enabled.
- Non-SQLite payloads with `.db`/`.sqlite` names are rejected earlier.

Regression risk:

- Production operators who relied on API restore must explicitly set `Backup:AllowApiRestore=true`; this is intentional.

### Audit Integrity

Files:

- `DocumentManagement.Api/Controllers/BackupController.cs`
- `DocumentManagement.Api/Controllers/SystemController.cs`
- `DocumentManagement.Api/Controllers/CatalogController.cs`

Changes:

- Backup download writes audit record when `IAuditLogRepository` is available.
- Restore writes audit records for:
  - blocked by config
  - successful restore
  - validation failure
- User create/update/delete writes audit records after successful mutation.
- Category/status create/update/delete writes audit records after successful mutation.

Risk reduced:

- Backup, restore, user administration, and catalog administration operations are now visible in application audit logs.

Regression risk:

- Backup, restore, user administration, and catalog administration now perform an extra audit insert. If audit insert fails, the operation can fail because audit integrity is treated as part of the operation.

### Password Change Protection

Files:

- `DocumentManagement.Contracts/Auth/ChangePasswordRequest.cs`
- `DocumentManagement.Application/Interfaces/IAuthService.cs`
- `DocumentManagement.Infrastructure/Services/AuthService.cs`
- `DocumentManagement.Api/Controllers/AuthController.cs`

Changes:

- Self-service password change now requires `OldPassword`.
- The current password is verified against the stored password hash before changing the caller's own password.
- Admin password reset behavior for other users remains available.

Risk reduced:

- Reduces impact from unattended authenticated sessions or stolen bearer tokens being used to silently rotate the victim's password.

Regression risk:

- Clients calling self-service password change must send `OldPassword`; admin reset flows are not affected.

### Login Endpoint Protection

Files:

- `DocumentManagement.Api/Program.cs`
- `DocumentManagement.Api/Controllers/AuthController.cs`
- `DocumentManagement.Api/appsettings.json`
- `DocumentManagement.Api/appsettings.Production.template.json`

Changes:

- Added ASP.NET Core fixed-window rate limiting.
- Applied the named `login` limiter to `POST /api/auth/login`.
- Default configuration allows 120 attempts per minute outside Production.
- Production template lowers the default to 20 attempts per minute.

Risk reduced:

- Adds an application-level control against credential stuffing and brute-force bursts.

Regression risk:

- Legitimate high-volume login bursts may receive `429 Too Many Requests`; limits are configurable.

### Authorization Correctness

File:

- `DocumentManagement.Api/Controllers/DocumentsController.cs`

Changes:

- Document read access is now scoped:
  - Admin can read all.
  - Non-admin can read documents assigned to their username.
  - Non-admin can read documents whose `ProcessingDepartment` matches their department claim.
- `GET /api/documents`, `GET /api/documents/search`, and `GET /api/documents/{id}` now apply this scope.

Risk reduced:

- Low-privilege users can no longer read every active document by default.

Regression risk:

- Existing clients/users may stop seeing documents without `ProcessingDepartment` or `AssignedTo` populated.
- Historical data may need backfill for department/assignment fields.

### Pagination and Endpoint Protection

File:

- `DocumentManagement.Api/Controllers/DocumentsController.cs`

Changes:

- Document search `pageSize` is capped at 200.
- Unpaged document list returns at most 200 scoped active documents.

Risk reduced:

- Reduces large-payload abuse and memory pressure.

Regression risk:

- Clients expecting `GET /api/documents` to return all records must use `/api/documents/search` with pagination.

## Verification

Build:

```powershell
dotnet build .\DocumentManagement.sln --no-restore
```

Result:

- Passed
- Warnings: 0
- Errors: 0

Tests:

```powershell
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj --no-build
```

Result:

- Passed: 43
- Failed: 0
- Skipped: 0
- Total: 43

## Remaining Must-Fix Items

Not completed in this pass:

1. Remove real/default secrets from checked-in config and deployment docs.
2. Add account lockout or progressive delay after repeated failed login attempts.
3. Add explicit authorization policies instead of repeated manual checks.
4. Add stronger backup restore schema/version validation beyond core table presence.
5. Add production monitoring for health, service status, disk, and backup freshness.

## Final Assessment

This hardening pass reduces the highest-risk API surfaces without changing the architecture. The most likely regression is document visibility: scoped read access may hide legacy documents that lack department or assignment data.
