# Pre-Pilot Security and Release Plan

Generated: 2026-05-11

## Executive Summary

Decision: **GO only after P0 gates are complete and validated**.

The system is appropriate for a controlled 5-user internal pilot, not public production. The highest residual application risks were document search count leakage and broad Manager/Publisher reporting. This pass applies minimal code changes to scope document search counts and reports by the same document-read rule already used by document and dashboard endpoints.

Concrete changes made in this pass:

- Staging now fails startup if `Jwt:Secret` is still the placeholder.
- Document search applies RBAC scope before `COUNT(*)` and pagination.
- Manager/Publisher report summary is scoped to readable documents.
- Added pilot operational check script.
- Added SQLite backup verification script.

## Risk Matrix

| ID | Risk | Level | Status | Pilot Decision |
| --- | --- | --- | --- | --- |
| R1 | Placeholder JWT secret in pilot/staging | Critical | Fixed in code, must configure real secret | P0 gate |
| R2 | Default seeded passwords still active | Critical | Operational gate | P0 gate |
| R3 | Unused seeded accounts active | High | Operational gate | P0 gate |
| R4 | API exposed without TLS/internal boundary | High | Operational gate | P0 gate |
| R5 | No daily backup/restore proof | High | Scripts/runbook added | P0 gate |
| R6 | Search `TotalCount` leaks hidden document volume | Medium | Fixed | Validate |
| R7 | Manager/Publisher reports expose broad aggregate data | Medium | Fixed by read scope | Validate |
| R8 | NuGet vulnerability audit incomplete | Medium | Open due network | P1 gate before expansion |
| R9 | Monitoring mostly manual | Medium | Script added | Accept for 5 users |
| R10 | SQLite scaling limits | Low for 5 users | Accepted only for pilot | P2 migration |

## P0 Remediation: Must Complete Before Pilot

### P0-1: Unique Pilot/Staging JWT Secret

Risk level: Critical

Rationale:

- A known JWT signing secret allows token forgery.

Implementation steps:

- Code now rejects placeholder `Jwt:Secret` in `Production` and `Staging`.
- Generate a unique secret with at least 32 characters.
- Supply it through environment/config for staging:

```powershell
$env:Jwt__Secret = "<unique-pilot-secret-at-least-32-chars>"
```

Or publish staging with:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\publish-api.ps1 `
  -Environment Staging `
  -Urls http://0.0.0.0:5034 `
  -DatabaseProvider Sqlite `
  -DatabasePath database/pilot-5users.db `
  -JwtSecret "<unique-pilot-secret-at-least-32-chars>"
```

Validation:

- Start with placeholder in Staging and confirm startup fails.
- Start with real secret and confirm `/health` returns `200`.

Rollback:

- Revert to prior artifact only if staging cannot start. Do not roll back to a placeholder secret for pilot.

### P0-2: Rotate Default Passwords

Risk level: Critical

Rationale:

- Default credentials are known from seed/test/deployment history.

Implementation steps:

- Log in as Admin on pilot.
- Create the five named pilot users or update existing seed users.
- Change passwords for `admin`, `manager`, `publisher`, and `staff`.
- Force users to store credentials through the agreed pilot channel.

Validation:

- Confirm old passwords fail for all default users.
- Confirm the five pilot users can log in.

Rollback:

- Keep one break-glass Admin credential sealed with the pilot owner.
- If lockout occurs, restore the pre-pilot database backup.

### P0-3: Disable Unused Seeded Accounts

Risk level: High

Rationale:

- Extra active accounts increase attack surface.

Implementation steps:

- In `/api/system/users`, set unused seed users `IsActive=false`.
- Keep only the five named pilot accounts active.

Validation:

- Attempt login for disabled users and confirm unauthorized.
- Confirm existing tokens for disabled users are rejected by active-user middleware.

Rollback:

- Re-enable account through Admin UI/API if the account is intentionally needed.

### P0-4: Internal-Only Or TLS Boundary

Risk level: High

Rationale:

- Bearer tokens and credentials must not traverse untrusted networks over plain HTTP.

Implementation steps:

- Preferred: put API behind IIS/reverse proxy with TLS.
- Acceptable for 5-user pilot: restrict API to isolated internal LAN and approved clients only.
- Firewall should allow only pilot client networks to the API port.

Validation:

- From approved client: `/health` succeeds.
- From non-approved network: API port is blocked.

Rollback:

- Disable firewall rule or stop service if unintended exposure is detected.

### P0-5: Dedicated Pilot Database

Risk level: High

Rationale:

- Pilot must not risk development or production data.

Implementation steps:

- Use `database/pilot-5users.db` or a staging SQL Server database.
- Do not point pilot at development `app.db`.

Validation:

- Check `appsettings.Staging.json`.
- Confirm database file path under pilot app folder.

Rollback:

- Stop service and restore previous pilot database backup.

### P0-6: Daily Backup And Restore Verification

Risk level: High

Rationale:

- A pilot is only acceptable if data can be recovered.

Implementation steps:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\install-backup-task.ps1 `
  -DatabasePath C:\QuanLyVanBan\Api\database\pilot-5users.db `
  -BackupDirectory C:\QuanLyVanBan\Backups `
  -Time 23:00 `
  -RetentionDays 30
```

Verify latest backup on a copy:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\verify-sqlite-restore.ps1 `
  -BackupPath C:\QuanLyVanBan\Backups\app-YYYYMMDD-HHMMSS.db `
  -SqliteAssemblyPath C:\QuanLyVanBan\Api\Microsoft.Data.Sqlite.dll
```

Validation:

- Backup file exists.
- Restore verifier passes header, core tables, and `PRAGMA integrity_check`.

Rollback:

- Stop service.
- Replace pilot DB with last verified backup.
- Start service and confirm `/health`.

### P0-7: Daily Operational Checks

Risk level: Medium

Rationale:

- Monitoring is manual for the 5-user pilot; checks must be repeatable.

Implementation steps:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\check-pilot-health.ps1 `
  -ApiBase http://PILOT_SERVER:5034 `
  -ServiceName DocumentManagement.Api.Pilot `
  -LogDirectory C:\QuanLyVanBan\Api\logs `
  -BackupDirectory C:\QuanLyVanBan\Backups
```

Validation:

- Script exits `0`.
- `/health`, service status, disk, backup freshness, and log scan are reported.

Rollback:

- If check fails, stop pilot intake until root cause is fixed or previous artifact/database is restored.

## P1 Remediation: Should Fix During Pilot

### P1-1: NuGet Vulnerability Audit

Risk level: Medium

Rationale:

- Local sandbox could not reach NuGet vulnerability feeds.

Implementation:

```powershell
dotnet restore .\DocumentManagement.sln
dotnet list .\DocumentManagement.sln package --vulnerable --include-transitive
```

Validation:

- No critical/high vulnerable packages remain without accepted exception.

Rollback:

- If package updates break build, revert package changes and document exception.

### P1-2: Pilot Issue Triage

Risk level: Medium

Rationale:

- Pilot feedback must not become uncontrolled feature expansion.

Implementation:

- Log each issue with role, endpoint/screen, expected/actual behavior, timestamp, severity.
- Only P0/P1 defects get code changes during stabilization.

Validation:

- Daily review of open pilot issues.

Rollback:

- Revert any pilot fix that worsens RBAC, startup, backup, or data integrity.

## P2 Remediation: Post-Pilot Scaling

### P2-1: Move SQLite To SQL Server

Risk level: Medium after pilot

Rationale:

- SQLite is acceptable for <=5 users but not the long-term concurrent production target.

Implementation:

- Create SQL Server staging database.
- Run migrator.
- Validate indexes and backups.

Validation:

- Full smoke test passes on SQL Server.

Rollback:

- Keep SQLite pilot DB read-only backup.

### P2-2: Centralize Authorization Policies

Risk level: Medium

Rationale:

- Manual role checks are prone to drift.

Implementation:

- Add policies: `AdminOnly`, `CatalogWrite`, `ReportRead`, `DocumentScopedRead`.
- Migrate controller helpers gradually.

Validation:

- Existing RBAC integration tests pass.

Rollback:

- Revert policy migration per controller.

## Security Fixes Applied

### JWT Secret Loading

Current behavior:

- Configuration binding supports environment override such as `Jwt__Secret`.
- Staging and Production now reject `CHANGE_THIS...` placeholder secrets.

### TotalCount Information Leak

Vulnerability:

- Before this fix, repository count returned all search matches before controller RBAC filtering.
- A non-admin could infer hidden document volume by comparing `TotalCount` with visible `Items`.

Secure query pattern:

```sql
SELECT COUNT(*)
FROM documents
WHERE is_active = 1
  AND (...search filters...)
  AND (
      LOWER(TRIM(assigned_to)) = LOWER(TRIM(@readScopeUsername))
      OR LOWER(TRIM(processing_department)) = LOWER(TRIM(@readScopeDepartment))
  );
```

RBAC-safe pagination rule:

- Apply authorization scope in SQL before `COUNT(*)`.
- Apply the same scope before `LIMIT/OFFSET` or SQL Server paging.
- Return `TotalCount` from the scoped count.
- Return `Items` from the scoped page.

Implemented files:

- `DocumentManagement.Application/Models/DocumentSearchRequest.cs`
- `DocumentManagement.Api/Controllers/DocumentsController.cs`
- `DocumentManagement.Infrastructure/Repositories/DocumentRepository.cs`

### Manager/Publisher Aggregate Reporting

Likely flaw:

- Reports were globally aggregating all active documents for Manager/Publisher.
- That is broader than least privilege unless explicitly intended.

Least-privilege fix:

- Admin sees global reports.
- Manager/Publisher sees assigned or same-department documents only.
- Staff remains forbidden from reports.

Implemented file:

- `DocumentManagement.Api/Controllers/ReportsController.cs`

Policy example for later cleanup:

```csharp
options.AddPolicy("ReportRead", policy =>
    policy.RequireRole("Admin", "Manager", "Publisher"));
```

Controller-level document-scope filtering should still be applied after policy approval because role approval alone does not determine document visibility.

## Operational Fixes Added

Files:

- `tools/check-pilot-health.ps1`
- `tools/verify-sqlite-restore.ps1`

Purpose:

- Make manual pilot monitoring repeatable.
- Verify SQLite backups on a copy before trusting restore.

## GO / NO-GO Checklist

GO only if all are true:

- [ ] Build passes.
- [ ] Tests pass.
- [ ] Staging/pilot startup passes.
- [ ] Staging/pilot smoke test passes.
- [ ] Real JWT secret configured.
- [ ] Default passwords rotated.
- [ ] Unused seeded accounts disabled.
- [ ] API is internal-only or behind TLS.
- [ ] Dedicated pilot database configured.
- [ ] Daily backup installed.
- [ ] Restore verification passes on a copy.
- [ ] Operational health script passes.
- [ ] Pilot owner and rollback owner assigned.

NO-GO if any are true:

- [ ] Placeholder secret is active.
- [ ] Default passwords still work.
- [ ] Backup is missing or unverified.
- [ ] `/health` fails.
- [ ] API exposed publicly over HTTP.
- [ ] Any unauthorized role can access admin/audit/backup/system APIs.

## Operational Runbook For 5-User Pilot

Daily:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\check-pilot-health.ps1 `
  -ApiBase http://PILOT_SERVER:5034 `
  -ServiceName DocumentManagement.Api.Pilot `
  -LogDirectory C:\QuanLyVanBan\Api\logs `
  -BackupDirectory C:\QuanLyVanBan\Backups
```

After each deployment:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\smoke-test.ps1 `
  -ApiBase http://PILOT_SERVER:5034
```

Weekly:

- Verify latest backup on a copy.
- Review audit logs for delete, backup, restore, user, and catalog actions.
- Review failed login patterns.

## Rollback Playbook

1. Stop pilot service:

```powershell
Stop-Service DocumentManagement.Api.Pilot
```

2. Restore previous API artifact.

3. Restore database only if data/schema corruption requires it.

4. Start service:

```powershell
Start-Service DocumentManagement.Api.Pilot
Invoke-WebRequest http://localhost:5034/health -UseBasicParsing
```

5. Run smoke test.

6. Notify pilot users.

## Production Readiness Gap Assessment

Still required before production:

- SQL Server deployment validation.
- Centralized monitoring/alerting.
- TLS enforced by IIS/reverse proxy or Kestrel.
- NuGet vulnerability audit with network access.
- Restore drill evidence.
- Formal authorization policies.
- Longer soak test with realistic data volume.

## Final Recommendation

Recommendation: **GO for 5-user internal pilot only after all P0 gates pass**.

Do not expand beyond the pilot until P1 items are closed and SQL Server/monitoring/TLS readiness is proven.
