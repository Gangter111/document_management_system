# Security Audit

Generated: 2026-05-11

## Scope

Security-only review focused on:

- JWT issuance and validation
- upload handling
- backup endpoints
- authorization leaks

No code changes were made.

## Executive Summary

The API has a baseline security posture: controllers are broadly protected with `[Authorize]`, only login is anonymous, JWT validation checks issuer/audience/signing key/lifetime, and backup/audit/system administration endpoints include explicit admin checks.

However, the system is not ready for pilot deployment without mitigation. The highest-risk issues are checked-in/default secrets, weak upload validation, high-risk backup restore behavior, missing old-password verification, and sensitive document/dashboard/catalog reads available to every authenticated user.

## JWT Findings

### SEC-JWT-001: Source-Controlled JWT Placeholder and Seed Credentials

- Severity: Critical
- Affected files:
  - `DocumentManagement.Api/appsettings.json`
  - `DocumentManagement.Api/appsettings.Production.template.json`
  - `DocumentManagement.Infrastructure/Data/DatabaseMigrator.cs`
- Evidence:
  - `Jwt:Secret` is present in checked-in config.
  - `AdminSeed:Password` is present in checked-in config.
  - Default users are seeded with known passwords such as `admin123`, `manager123`, `publisher123`, and `staff123`.
- Exploit scenario:
  - If a deployed environment uses checked-in/default values, an attacker can log in with default credentials or forge JWTs if the signing key is known.
- Mitigation:
  - Remove deployable secrets/default passwords from source-controlled config.
  - Supply `Jwt__Secret` and initial admin password through environment variables, Windows service settings, IIS app settings, or a secret store.
  - Rotate all default user passwords before pilot.
  - Fail startup if seed passwords remain default in Production.

### SEC-JWT-002: HTTPS Metadata Disabled

- Severity: High
- Affected file:
  - `DocumentManagement.Api/Program.cs`
- Evidence:
  - `options.RequireHttpsMetadata = false`
- Exploit scenario:
  - If the API is deployed over plain HTTP, credentials and bearer tokens can be intercepted on the network.
- Mitigation:
  - Set `RequireHttpsMetadata = true` in Production.
  - Add HTTPS redirection/HSTS when the API terminates TLS.
  - If TLS is terminated upstream, document and enforce that topology.

### SEC-JWT-003: No Login Rate Limiting or Lockout

- Severity: High
- Affected files:
  - `DocumentManagement.Api/Controllers/AuthController.cs`
  - `DocumentManagement.Infrastructure/Services/AuthService.cs`
- Exploit scenario:
  - An attacker can brute-force passwords against `POST /api/auth/login`, especially dangerous while default accounts exist.
- Mitigation:
  - Add ASP.NET Core rate limiting for login by username and source IP.
  - Add account lockout or progressive delay after repeated failures.
  - Log failed login attempts without logging passwords.

### SEC-JWT-004: Password Change Does Not Require Old Password

- Severity: High
- Affected files:
  - `DocumentManagement.Api/Controllers/AuthController.cs`
  - `DocumentManagement.Infrastructure/Services/AuthService.cs`
- Evidence:
  - `ChangePassword` accepts `UserId` and `NewPassword`.
  - Existing password is not verified for self-service changes.
- Exploit scenario:
  - A stolen token can permanently take over the victim account by changing its password without knowing the current password.
- Mitigation:
  - Require and verify `OldPassword` for self-service password changes.
  - Split admin password reset into a separate admin-only endpoint.
  - Audit both self-service password changes and admin resets.

### SEC-JWT-005: Active-User Middleware Is Good but Adds No Token Version Check

- Severity: Medium
- Affected file:
  - `DocumentManagement.Api/Program.cs`
- Evidence:
  - Middleware verifies `Users.Id` and `IsActive = 1` on each authenticated request.
- Exploit scenario:
  - Deactivated users are blocked, but password changes or role changes do not invalidate already-issued tokens until expiration.
- Mitigation:
  - Add a security stamp/token version claim and compare it to the database.
  - Keep token lifetime short for pilot if token revocation is not implemented.

## Upload Findings

### SEC-UPLOAD-001: PDF Extraction Validates Extension Only

- Severity: High
- Affected file:
  - `DocumentManagement.Api/Controllers/DocumentsController.cs`
- Evidence:
  - `extract-pdf` checks only `Path.GetExtension(file.FileName) == ".pdf"`.
  - No MIME type check or file signature check is performed before writing and parsing.
- Exploit scenario:
  - An attacker uploads a non-PDF or parser-hostile payload named `file.pdf`, causing parser errors, resource exhaustion, or exploitation of PDF parsing dependencies.
- Mitigation:
  - Validate `ContentType` is `application/pdf`.
  - Validate the file header starts with `%PDF-`.
  - Reject files that exceed configured limits before copying.
  - Consider scanning/sandboxing parsed documents.

### SEC-UPLOAD-002: PDF Upload Writes to Temp Before Content Validation

- Severity: Medium
- Affected file:
  - `DocumentManagement.Api/Controllers/DocumentsController.cs`
- Exploit scenario:
  - Malicious or junk files are written to disk before content validation, increasing temp-storage abuse risk.
- Mitigation:
  - Validate basic metadata and file header from the request stream before writing the full file.
  - Set a dedicated temp directory with restricted ACLs and cleanup policy.

### SEC-UPLOAD-003: Restore Upload Extension Validation Is Insufficient

- Severity: High
- Affected file:
  - `DocumentManagement.Api/Controllers/BackupController.cs`
- Evidence:
  - Restore accepts `.db` and `.sqlite`.
  - Validation checks whether core table names exist, but not full schema, constraints, user/role integrity, or SQLite header before opening.
- Exploit scenario:
  - A compromised admin account uploads a crafted SQLite database that passes minimal table checks and replaces live data with manipulated users/roles/documents.
- Mitigation:
  - Validate SQLite header before opening.
  - Validate schema version, required columns, indexes, role/user constraints, and migration level.
  - Require an out-of-band confirmation or maintenance mode for restore.
  - Prefer offline restore scripts over API restore in pilot.

## Backup Endpoint Findings

### SEC-BACKUP-001: Backup Restore API Can Overwrite Live Database

- Severity: Critical
- Affected file:
  - `DocumentManagement.Api/Controllers/BackupController.cs`
- Evidence:
  - `POST /api/backup/restore` copies uploaded file over the configured database path.
- Exploit scenario:
  - Any stolen admin token enables full database replacement, including users, roles, and audit history.
- Mitigation:
  - Disable restore API for pilot unless absolutely required.
  - Move restore to an offline operational script requiring server access.
  - If retained, require maintenance mode, additional confirmation, strict schema validation, and audit logging.

### SEC-BACKUP-002: Backup Download Creates Files Under Content Root

- Severity: High
- Affected file:
  - `DocumentManagement.Api/Controllers/BackupController.cs`
- Evidence:
  - Backups are written to `ContentRootPath/backups`.
- Exploit scenario:
  - If static file serving or server misconfiguration later exposes that directory, database backups can leak.
- Mitigation:
  - Store backups outside content root with restricted filesystem ACLs.
  - Use a configured backup directory.
  - Delete one-off download files after transfer or use retention controls.

### SEC-BACKUP-003: Backup/Restore Actions Are Not Audited

- Severity: High
- Affected file:
  - `DocumentManagement.Api/Controllers/BackupController.cs`
- Exploit scenario:
  - A privileged user can download or restore data without an application audit record. Incident response cannot reliably identify who performed the action.
- Mitigation:
  - Write audit logs for backup download, restore attempt, restore success/failure, actor, source IP, and timestamp.
  - Keep audit records outside the restored database if possible, or forward them to server logs/SIEM.

### SEC-BACKUP-004: Backup Health Leaks Provider and Database Existence to Admins

- Severity: Low
- Affected file:
  - `DocumentManagement.Api/Controllers/BackupController.cs`
- Evidence:
  - `GET /api/backup/health` returns provider and database existence.
- Exploit scenario:
  - Low risk because endpoint is admin-only; still useful reconnaissance if an admin token is compromised.
- Mitigation:
  - Keep admin-only.
  - Avoid returning filesystem paths or detailed internal locations.

## Authorization Leak Findings

### SEC-AUTHZ-001: All Authenticated Users Can Read All Documents

- Severity: High
- Affected file:
  - `DocumentManagement.Api/Controllers/DocumentsController.cs`
- Evidence:
  - `GET /api/documents`, `GET /api/documents/search`, and `GET /api/documents/{id}` require authentication but do not enforce role, department, ownership, or confidentiality restrictions.
- Exploit scenario:
  - A low-privilege staff account can enumerate all active documents, including confidential documents assigned to other departments.
- Mitigation:
  - Enforce document read scope by role, department, assigned user, and confidentiality level.
  - Apply filtering in repository/service, not only in controller.
  - Add tests proving staff users cannot read out-of-scope documents.

### SEC-AUTHZ-002: All Authenticated Users Can Read Dashboard and Lookups

- Severity: Medium
- Affected files:
  - `DocumentManagement.Api/Controllers/DashboardController.cs`
  - `DocumentManagement.Api/Controllers/LookupsController.cs`
- Exploit scenario:
  - Staff users can view organization-wide dashboard aggregates and lookup metadata that may reveal operational volume or categories.
- Mitigation:
  - Decide whether these are intentionally global.
  - If not, scope dashboard by role/department and restrict sensitive lookup fields.

### SEC-AUTHZ-003: Catalog Read Endpoints Allow Inactive Data Disclosure

- Severity: Medium
- Affected file:
  - `DocumentManagement.Api/Controllers/CatalogController.cs`
- Evidence:
  - `GET /api/catalog/categories?includeInactive=true` and `GET /api/catalog/statuses?includeInactive=true` are available to any authenticated user.
- Exploit scenario:
  - Any authenticated user can enumerate inactive/internal catalog entries.
- Mitigation:
  - Restrict `includeInactive=true` to Admin/Manager.
  - Leave active-only lookup reads open if business-appropriate.

### SEC-AUTHZ-004: Reports Are Available to Publisher

- Severity: Medium
- Affected file:
  - `DocumentManagement.Api/Controllers/ReportsController.cs`
- Evidence:
  - `RequireReportPermission` permits `Admin`, `Manager`, and `Publisher`.
- Exploit scenario:
  - Publisher users can view organization-wide document statistics across departments.
- Mitigation:
  - Confirm this is a business requirement.
  - If not, restrict reports to Admin/Manager or department-scope Publisher reports.

### SEC-AUTHZ-005: Manual Role Checks Increase Future Leak Risk

- Severity: Medium
- Affected files:
  - `DocumentsController.cs`
  - `BackupController.cs`
  - `SystemController.cs`
  - `CatalogController.cs`
  - `ReportsController.cs`
  - `AuditLogsController.cs`
- Exploit scenario:
  - A future endpoint may be added with `[Authorize]` but without the correct manual role check, exposing sensitive operations to all authenticated users.
- Mitigation:
  - Define named policies such as `AdminOnly`, `BackupAdmin`, `CatalogWrite`, `ReportRead`, `DocumentReadScoped`, `DocumentWriteScoped`.
  - Apply declaratively with `[Authorize(Policy = "...")]`.
  - Keep resource-specific department checks in services.

## Positive Controls Observed

- All controllers reviewed use `[Authorize]` at class level except login explicitly uses `[AllowAnonymous]`.
- JWT validates issuer, audience, signing key, and lifetime.
- Production startup rejects placeholder JWT secrets.
- Password hashes use BCrypt.
- Backup download, restore, and backup health require admin role.
- System user administration requires admin role.
- Audit log reads require admin role.
- Global exception handler avoids exposing unhandled exception details.
- Swagger is only enabled in development.

## Priority Fix List

Must fix before pilot:

1. Move JWT secret and default credentials out of checked-in config; rotate all default accounts.
2. Enforce TLS or documented trusted TLS termination; enable production HTTPS metadata validation.
3. Add login rate limiting/lockout.
4. Require old-password verification for self-service password change.
5. Harden PDF validation with MIME and magic-header checks.
6. Disable or heavily restrict API restore; prefer offline restore.
7. Add audit events for backup download/restore.
8. Implement document read authorization scope.

Should fix soon:

1. Restrict inactive catalog reads.
2. Confirm or narrow Publisher report access.
3. Add token version/security stamp for token revocation after password/role changes.
4. Replace manual role checks with authorization policies.

