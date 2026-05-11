# Five User Trial Evaluation

Generated: 2026-05-11

## Decision

The system is **okay to try with a controlled internal team of 5 people**, provided the mandatory gates below are completed first.

It is **not ready for broad production rollout** and should not be internet-exposed.

Recommended trial mode:

- internal network only
- 5 named users
- staging or pilot environment
- daily backup
- daily log review
- clear rollback owner

## Current Verification Baseline

Latest checks completed:

- `dotnet build .\DocumentManagement.sln --no-restore`
  - Passed
  - Errors: 0
  - Warning caveat: `NU1900` because this environment cannot reach NuGet vulnerability feeds
- `dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj`
  - Passed: 44
  - Failed: 0
- API startup health check:
  - `/health` returned `200`
- Staging artifact validation:
  - Staging package published
  - Staging API started
  - Staging `/health` returned `200`
  - Smoke test passed end to end

## Why A 5-User Trial Is Reasonable

The project has enough safeguards for a small controlled pilot:

- JWT authentication is active.
- Production rejects placeholder JWT secrets.
- Deactivated users are rejected at request time.
- Admin-only endpoints exist for system, audit, and backup operations.
- Backup restore is disabled by default in staging/production templates.
- Swagger is Development-only.
- Global exception handler avoids exposing internal exception details.
- Upload validation was hardened for PDF extraction.
- Backup restore validation was improved for SQLite header/core tables.
- RBAC was tightened for dashboard/document visibility.
- Integration/controller tests cover major controller surfaces.
- Staging publish and smoke test path exists.

## Mandatory Gates Before Letting 5 Users In

- [ ] Use a unique pilot/staging `Jwt__Secret`.
- [ ] Rotate all default passwords.
- [ ] Disable unused default accounts.
- [ ] Keep API internal-only or behind TLS.
- [ ] Confirm only the 5 pilot users can access the client/API.
- [ ] Use a dedicated pilot database, not development or production data.
- [ ] Run manual backup before trial start.
- [ ] Confirm daily backup job is active.
- [ ] Test restore on a copy, not the live pilot database.
- [ ] Assign one operator to check logs and `/health` daily.
- [ ] Keep rollback artifact and database backup available.

## Recommended 5-User Mix

| Count | Role | Purpose |
| --- | --- | --- |
| 1 | Admin | User management, audit review, backup health |
| 1 | Manager | Department review/update and reporting |
| 1 | Publisher | Publishing/update workflow |
| 2 | Staff | Document creation and read-scope validation |

## Known Risks During Trial

### RBAC Search Count Leakage

Severity: Medium

`DocumentsController` filters returned search items by read scope, but `TotalCount` can still reflect the repository count before RBAC filtering.

Trial impact:

- A user may infer that more documents exist than they can see.

Mitigation for 5-user trial:

- Accept temporarily for internal pilot, or avoid exposing sensitive cross-department volume during the trial.
- Fix before wider rollout.

### Reports May Be Too Broad

Severity: Medium

Reports are available to Admin, Manager, and Publisher and may aggregate all active documents.

Trial impact:

- Managers/Publishers may see global aggregate data.

Mitigation:

- Confirm this is acceptable for the 5 pilot users.
- If not acceptable, scope reports the same way dashboard is scoped before trial.

### NuGet Vulnerability Audit Not Confirmed

Severity: Medium

Current environment cannot reach NuGet vulnerability feeds, causing `NU1900`.

Trial impact:

- Build passes, but dependency vulnerability status is not confirmed.

Mitigation:

- Re-run restore/build/audit on a machine with NuGet access before live pilot.

### Monitoring Is Basic

Severity: Medium

Only health endpoint, logs, and manual checks are currently available.

Trial impact:

- Failures may be noticed by users before operators unless daily checks are performed.

Mitigation:

- Daily `/health`, service status, disk, logs, and backup freshness checks are mandatory.

### SQLite Limitations

Severity: Low for 5 users, higher if expanded

SQLite can work for a small pilot but is not the long-term multi-user production target.

Mitigation:

- Use SQLite only for this small trial.
- Move to SQL Server before larger rollout.

## Go / No-Go

Recommendation: **GO for a 5-user controlled internal trial after mandatory gates are complete**.

No-go if any are true:

- Placeholder JWT secret is still configured.
- Default passwords still work.
- Backup is not configured.
- Restore has not been tested on a copy.
- API is exposed publicly without TLS.
- `/health` fails.
- Pilot users include departments whose data must remain strictly isolated and report scoping has not been confirmed.

## Final Answer

Yes, it is reasonable to try this system with a team of 5 people in a controlled pilot. Treat it as a monitored staging/pilot deployment, not production. Do not expand beyond 5 users until the RBAC count/reporting questions, vulnerability audit, restore drill, and monitoring gaps are resolved.
