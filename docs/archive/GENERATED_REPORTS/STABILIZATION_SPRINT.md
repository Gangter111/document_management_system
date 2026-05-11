# Stabilization Sprint

Generated: 2026-05-11

## Scope

This sprint is restricted to stabilization work only.

Allowed work:

- production stability fixes
- monitoring and operational diagnostics
- logging improvements
- RBAC correctness fixes
- bug fixes
- performance tuning
- pilot feedback fixes
- documentation updates for operation and support

Not allowed:

- new features
- broad architecture rewrites
- UI redesign
- new business workflows
- speculative refactoring
- database model expansion unless required for a confirmed bug or operational blocker

## Change Policy

Every code change must be:

- minimal
- reversible
- covered by focused tests where practical
- documented in the relevant operational or hardening report
- verified by build, tests, and startup health check

Before code changes:

1. State the root cause.
2. State the proposed minimal fix.
3. State regression risks.
4. Apply the patch.

After code changes:

1. Build solution.
2. Run affected tests, then full test project when feasible.
3. Validate startup with `/health`.
4. Update this sprint document or a focused report.

## Priority Order

1. Critical security or RBAC correctness defects.
2. Startup, deployment, or backup/restore blockers.
3. Data integrity or audit integrity defects.
4. Monitoring, logging, and diagnostics gaps.
5. Performance issues affecting pilot users.
6. Low-risk pilot feedback fixes.

## Stabilization Backlog

| Priority | Item | Status | Notes |
| --- | --- | --- | --- |
| P0 | Verify no unauthorized access to admin, audit, backup, system APIs | Open | Covered by existing tests, continue validating pilot reports |
| P0 | Keep backup restore API disabled in staging/pilot unless supervised | Open | `Backup:AllowApiRestore=false` in staging/production templates |
| P1 | Fix document search `TotalCount` RBAC leakage | Open | Known RBAC review item; needs query-level scoped count |
| P1 | Add operational log review checklist for pilot support | Open | Daily support process required |
| P1 | Confirm NuGet vulnerability audit with network access | Open | Local sandbox cannot reach vulnerability feed |
| P2 | Centralize repeated role checks into policies | Deferred | Not during stabilization unless drift causes a bug |
| P2 | Expand health checks for disk/storage/backup freshness | Open | Operational monitoring improvement |
| P2 | Add restore drill evidence to pilot checklist | Open | Required before expanding pilot |

## Pilot Feedback Intake

Each pilot issue should be recorded with:

- reporter
- role
- endpoint or screen
- expected result
- actual result
- timestamp
- severity
- reproducibility
- screenshots/log references if available

Severity:

- P0: data loss, security leak, startup failure, backup failure
- P1: core workflow blocked for one or more pilot users
- P2: degraded performance, confusing but workable behavior
- P3: cosmetic or documentation issue

## Verification Baseline

Current expected baseline:

- solution builds
- test project passes
- API starts
- `/health` returns `200`
- staging smoke test passes before pilot use

## Operational Watchlist

During stabilization, review daily:

- Windows Service status
- `/health`
- API logs
- audit logs for destructive actions
- failed login patterns
- disk free space
- backup freshness
- database file size or SQL Server database growth

## Regression Risk Guidance

High-risk areas:

- authorization and document visibility
- backup/restore
- database migration/startup path
- seeded/default users
- PDF upload parsing
- password change and login throttling

For these areas, prefer one focused fix per change and run full tests afterward.

## Exit Criteria

Stabilization sprint can close when:

- no P0/P1 pilot issues remain open
- latest build and tests pass
- latest startup health check passes
- backup restore drill has been completed on a copy
- NuGet vulnerability audit has been run with network access
- pilot runbook and operational readiness docs reflect current behavior
