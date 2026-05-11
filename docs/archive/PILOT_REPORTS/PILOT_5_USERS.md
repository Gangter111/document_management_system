# 5-User Pilot Plan

Generated: 2026-05-11

## Pilot Scope

Pilot size: **5 users**

Recommended duration: **5 business days**

Deployment target:

- Staging or isolated internal pilot server
- API not exposed to the public internet
- SQLite acceptable for this pilot size if backup/restore is tested
- SQL Server preferred if the pilot is expected to become production without rebuild

## Pilot User Mix

Use real pilot accounts, not default seeded passwords.

Recommended account set:

| User | Role | Purpose |
| --- | --- | --- |
| Pilot Admin | Admin | User management, delete/restore checks, backup visibility |
| Department Manager | Manager | Review/update department documents and reports |
| Publisher | Publisher | Publish/update document metadata |
| Staff 1 | Staff | Create documents and verify department scoping |
| Staff 2 | Staff | Cross-user and assigned-document validation |

## Mandatory Gates Before Pilot

- [ ] Unique staging/pilot `Jwt__Secret` configured.
- [ ] Default passwords rotated.
- [ ] Unused default accounts disabled.
- [ ] API reachable only from approved internal network or over TLS.
- [ ] `/health` passes from server.
- [ ] `/health` passes from at least one pilot client machine.
- [ ] Backup job configured.
- [ ] Manual backup completed.
- [ ] Restore tested on a non-production copy.
- [ ] Rollback artifact available.
- [ ] Pilot owner and support contact assigned.

## Day 0 Setup

1. Publish API using staging path:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\publish-api.ps1 `
  -Environment Staging `
  -Urls http://0.0.0.0:5034 `
  -DatabaseProvider Sqlite `
  -DatabasePath database/pilot-5users.db `
  -JwtSecret "<unique-pilot-secret-at-least-32-chars>"
```

2. Install staging/pilot service with a distinct name:

```powershell
powershell -ExecutionPolicy Bypass -File .\install-service.ps1 `
  -ServiceName DocumentManagement.Api.Pilot `
  -DisplayName "Document Management API - Pilot" `
  -Environment Staging
```

3. Run smoke test:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\smoke-test.ps1 `
  -ApiBase http://PILOT_SERVER:5034
```

4. Publish WPF client pointing at pilot API.

5. Create or update the five pilot accounts.

6. Rotate all temporary passwords after first login.

## Pilot Test Scenarios

### Authentication

- [ ] Each pilot user can log in.
- [ ] Invalid password is rejected.
- [ ] Deactivated user token is rejected.
- [ ] Self-service password change requires current password.

### RBAC

- [ ] Admin can access system users, audit logs, backup health.
- [ ] Staff cannot access system users.
- [ ] Staff cannot access audit logs.
- [ ] Staff cannot delete documents.
- [ ] Publisher cannot delete documents.
- [ ] Manager cannot delete documents.
- [ ] Non-admin dashboard only shows readable documents.
- [ ] Non-admin users cannot move documents to another department.

### Document Workflow

- [ ] Staff creates a document.
- [ ] Publisher updates metadata in same department.
- [ ] Manager updates/reviews document in same department.
- [ ] Admin deletes test document.
- [ ] Deleted document is no longer readable.

### Upload Protection

- [ ] Valid PDF extraction succeeds or returns configured service unavailable if OCR is unavailable.
- [ ] Non-PDF file renamed to `.pdf` is rejected.
- [ ] Oversized upload is rejected.

### Backup / Restore

- [ ] Backup health is admin-only.
- [ ] Backup download is admin-only.
- [ ] API restore remains disabled unless explicitly testing restore in maintenance window.
- [ ] Script backup runs successfully.
- [ ] Restore test is performed outside the live pilot database.

## Daily Operations During Pilot

Morning:

- [ ] Check Windows Service status.
- [ ] Check `/health`.
- [ ] Confirm disk free space above 20%.
- [ ] Confirm previous backup exists.

End of day:

- [ ] Review API logs.
- [ ] Review audit logs for destructive actions.
- [ ] Record user issues.
- [ ] Record any failed login or authorization complaints.

## Pilot Success Criteria

Pilot is successful only if all are true:

- [ ] No startup failures.
- [ ] No data loss.
- [ ] Daily backups complete.
- [ ] Restore procedure is proven on a copy.
- [ ] No unauthorized role can access admin-only APIs.
- [ ] No cross-department dashboard/document leak is observed.
- [ ] Core document create/update/delete workflow works.
- [ ] Users can complete expected work without manual database intervention.
- [ ] Support issues are documented and triaged.

## Stop / Rollback Criteria

Stop pilot immediately if any occur:

- Unauthorized user can access system, audit, backup, or unrelated department data.
- Backup fails for more than one business day.
- `/health` fails and cannot be restored quickly.
- Database corruption or unexplained data loss occurs.
- API service repeatedly crashes.
- Logs show repeated unhandled exceptions in core workflows.

Rollback:

```powershell
Stop-Service DocumentManagement.Api.Pilot
```

- Restore previous API artifact.
- Restore database only if required and approved.
- Start service.

```powershell
Start-Service DocumentManagement.Api.Pilot
Invoke-WebRequest http://localhost:5034/health -UseBasicParsing
```

## Pilot Go / No-Go

Current recommendation: **GO for 5-user controlled internal pilot after mandatory gates are completed**.

Do not expand beyond 5 users until:

- RBAC search `TotalCount` leakage is fixed or accepted.
- Monitoring ownership is formalized.
- Backup restore is tested and documented.
- Default users are rotated or disabled.
