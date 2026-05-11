# Stabilization Policy

**Scope**: Permitted work during stabilization phase  
**Last Updated**: 2026-05-11

---

## Purpose

This document defines what work is allowed and how to execute it during the stabilization phase following pilot feedback and development freeze.

---

## What Is Allowed During Stabilization

✅ **Allowed**:
- Production stability fixes (crashes, hangs, data corruption)
- Security defect fixes (authorization, upload, backup/restore)
- Monitoring and operational diagnostics
- Logging and error handling improvements
- RBAC correctness fixes
- Bug fixes in existing features
- Performance tuning (not refactoring)
- Pilot user feedback fixes (within scope)
- Documentation updates for operations

❌ **NOT Allowed**:
- New features
- Broad architecture rewrites
- UI redesign
- New business workflows
- Speculative refactoring
- Database model expansion (unless required for confirmed bug/blocker)
- Dependency upgrades (unless security-critical)
- Infrastructure changes

---

## Change Procedure

### Before Code Changes

State:
1. **Root cause** - Why does this problem exist?
2. **Proposed minimal fix** - What's the smallest change that fixes it?
3. **Regression risks** - What could break?

### Executing Changes

1. **Create focused branch** (if using VCS)
   ```
   git checkout -b stab/fix-description
   ```

2. **Make minimal change**
   - Keep change size small (< 50 lines if possible)
   - Avoid unrelated cleanup

3. **Add focused test** (if practical)
   - Cover the specific fix
   - Avoid broad test refactoring

4. **Verify build**
   ```powershell
   dotnet build .\DocumentManagement.sln --no-restore
   ```

5. **Run affected tests**
   ```powershell
   dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj
   ```

6. **Validate startup**
   ```powershell
   dotnet run --project .\DocumentManagement.Api\DocumentManagement.Api.csproj
   ```

7. **Verify `/health` endpoint**
   ```
   http://localhost:5033/health
   ```

### After Code Changes

- Update relevant documentation (ops guide, deployment checklist, hardening report)
- Record change in sprint backlog
- Get code review from 1+ team member
- Merge to main branch

---

## Priority Order

When multiple fixes are available, prioritize:

1. **P0: Critical blockers**
   - Startup failures
   - Data corruption risks
   - Security defects (unauthorized access, injection, etc.)
   - Backup/restore failures

2. **P1: High-priority defects**
   - RBAC correctness issues
   - Pilot-blocking bugs
   - Monitoring/alerting gaps
   - Deployment blockers

3. **P2: Performance/UX improvements**
   - Slow endpoints
   - Pilot feedback (non-blocking)
   - Documentation fixes

4. **P3: Nice-to-haves**
   - Error message improvements
   - Logging verbosity
   - Long-term architectural improvements

---

## Stabilization Backlog Example

| ID | Priority | Issue | Status | Owner | Notes |
|----|----------|-------|--------|-------|-------|
| S-001 | P0 | API startup fails with invalid JWT | Fixed | Backend | Blocking pilot |
| S-002 | P0 | Backup restore corrupts SQLite | Fixed | Backend | Was critical risk |
| S-003 | P1 | Document search leaks count | Open | Backend | RBAC issue |
| S-004 | P1 | No rate limiting on login | Open | Security | Affects brute-force |
| S-005 | P2 | Dashboard slow with 1000 docs | Open | Backend | Performance tune |
| S-006 | P3 | Error messages could be clearer | Deferred | Backend | Nice-to-have |

---

## Exit Criteria for Stabilization

Stabilization phase can close when:

- ✅ No P0/P1 pilot issues remain open
- ✅ Build passes with 0 errors
- ✅ Tests pass (44+ tests)
- ✅ Startup health check passes
- ✅ Backup/restore verified working
- ✅ Monitoring operational
- ✅ Deployment docs updated
- ✅ Operations team trained

---

## Regression Testing Checklist

For **high-risk** changes, run full test suite:

```powershell
# Full suite
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj --logger:console

# Look for test failures
# Zero failures expected after high-risk change
```

Areas requiring extra care:

- ✅ Authorization and document visibility
- ✅ Backup/restore operations
- ✅ Database startup and migrations
- ✅ Default user seeding
- ✅ PDF upload and parsing
- ✅ Password change and login

---

## Operational Monitoring During Stabilization

Continue daily monitoring:

- Windows Service status
- `/health` endpoint
- API logs (errors/warnings)
- Audit logs (destructive operations)
- Failed login patterns
- Disk free space (> 20%)
- Backup freshness (< 24 hours old)
- Database file size growth

---

**Related**: [Operations Guide](OPERATIONAL_GUIDE.md) | [Deployment Checklist](../deployment/CHECKLIST.md) | [Risk Matrix](../security/RISK_MATRIX.md)
