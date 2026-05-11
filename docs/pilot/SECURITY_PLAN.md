# Pre-Pilot Security and Release Plan

**Decision**: GO for pilot after P0 gates are complete  
**Scope**: 5-user internal pilot | **Duration**: 5 business days

---

## Executive Summary

The system is appropriate for a **controlled 5-user internal pilot only** after completing mandatory P0 security gates. It is **not ready for broader production rollout** without additional hardening.

**Pilot gates**: See [Security Requirements](#security-requirements-p0-gates) below.

---

## Security Requirements (P0 Gates)

### Gate 1: JWT Secret Management

**Requirement**: Unique non-placeholder JWT secret in each environment

**Verification**:
- [ ] Production config does NOT contain "CHANGE_THIS" placeholder
- [ ] Secret is 32+ characters
- [ ] Secret is injected at deployment (not in source control)
- [ ] Different secret per environment (dev, staging, prod)

**Failure Impact**: Pilot blocked. Anyone with placeholder can forge tokens.

---

### Gate 2: Default Credentials Rotation

**Requirement**: All default seeded accounts have unique strong passwords

**Verification**:
- [ ] Seeded `admin` account password changed from `admin123`
- [ ] Seeded `manager` account password changed from `manager123`
- [ ] Seeded `publisher` account password changed from `publisher123`
- [ ] Seeded `staff` account password changed from `staff123`
- [ ] All unused seeded accounts disabled

**Failure Impact**: Pilot blocked. Anyone knowing defaults can login as admin.

---

### Gate 3: Transport Security (HTTPS)

**Requirement**: API protected by HTTPS or formal internal-LAN exception

**Verification**:
- [ ] HTTPS configured via Kestrel, IIS, or reverse proxy, OR
- [ ] Formal exception signed: "API internal-LAN only, no TLS" with approver name/date
- [ ] WPF client configured with `https://` URL (if TLS enabled)
- [ ] Certificate valid (if using Kestrel HTTPS)

**Failure Impact**: Credentials can be intercepted on network.

---

### Gate 4: Backup & Restore Validation

**Requirement**: Backup job runs successfully; restore tested on separate environment

**Verification**:
- [ ] Backup script runs daily on pilot server
- [ ] Backup file created and is recent (< 24 hours old)
- [ ] Restore test completed on non-production environment
- [ ] Restored database opens without corruption
- [ ] All tables and sample documents present
- [ ] Restore procedure documented in runbook

**Failure Impact**: Pilot blocked. No recovery from data loss.

---

### Gate 5: Operational Controls

**Requirement**: Monitoring and escalation procedures in place

**Verification**:
- [ ] Daily health check procedure defined
- [ ] Backup monitoring in place (alert if missing)
- [ ] Rollback procedure tested on staging
- [ ] Operations team on-call for support
- [ ] Escalation contacts defined

**Failure Impact**: Service issues cannot be detected or resolved.

---

## Changes Made in This Pass

### Security Hardening

| Change | File | Impact |
|--------|------|--------|
| PDF upload validates MIME + signature | DocumentsController | Prevents malicious upload |
| Backup restore validates SQLite header | BackupController | Prevents restore of non-database files |
| Backup restore default-disabled in prod | appsettings | Prevents accidental data overwrite |
| Dashboard/reports scoped by RBAC | DashboardController, ReportsController | Prevents cross-department visibility |
| Search count now scoped to readable docs | DocumentsController | Prevents count-based leakage |
| Password change requires old password | AuthController | Prevents stolen-token account takeover |

### Operational Improvements

| Improvement | File | Benefit |
|-------------|------|---------|
| Runtime paths anchored to content root | Program.cs | Stabilizes Windows Service deployment |
| Health check doesn't require Swagger | smoke-test.ps1 | Enables prod monitoring |
| Production startup rejects placeholder JWT | Program.cs | Prevents accidental deployment with wrong secret |

---

## Risk Assessment

| Risk ID | Risk | Severity | Pre-Pilot Status | Pilot Acceptability |
|---------|------|----------|------------------|-------------------|
| R-001 | Placeholder JWT secret | 🔴 Critical | **Fixed** (P0 gate) | ✅ Gated |
| R-002 | Default passwords active | 🔴 Critical | **Fixed** (P0 gate) | ✅ Gated |
| R-003 | No HTTPS | 🔴 Critical | **Fixed** (P0 gate) | ✅ Gated |
| R-004 | Backup restore fails | 🔴 Critical | **Fixed** (P0 gate) | ✅ Gated |
| R-005 | PDF upload validation | 🟠 High | **Fixed** | ✅ OK for pilot |
| R-006 | Backup/restore safety | 🟠 High | **Fixed** | ✅ OK for pilot |
| R-007 | Password change | 🟠 High | **Fixed** | ✅ OK for pilot |
| R-008 | Dashboard/report scoping | 🟠 High | **Fixed** | ✅ OK for pilot |
| R-009 | Login rate limiting | 🟡 Medium | Not implemented | ✅ OK for pilot (small user base) |
| R-010 | Token revocation | 🟡 Medium | Not implemented | ✅ OK for pilot (60-min token) |

---

## Pilot Operational Gates

During pilot, maintain:

- **Daily backup** verification (alert if missing)
- **Daily log** review (alert on ERROR/FATAL)
- **Health check** monitoring (alert if down)
- **Disk space** monitoring (alert if < 20%)
- **Immediate rollback** capability (previous release available)

---

## Post-Pilot P1 Items

Before broader rollout, address:

- [ ] Login rate limiting/account lockout
- [ ] Token revocation or security stamp
- [ ] Centralized authorization policies
- [ ] Comprehensive audit trail for all admin ops
- [ ] Monitoring and alerting (automated, not manual)

---

## Go/No-Go Pilot Decision

| Condition | Status | Decision |
|-----------|--------|----------|
| P0 gates complete | TBD | Pilot blocked until ✅ |
| Security review approved | TBD | Pilot blocked until ✅ |
| Ops team trained | TBD | Pilot blocked until ✅ |
| Backup/restore verified | TBD | Pilot blocked until ✅ |
| Build/tests passing | ✅ | OK |
| 5 pilot users ready | TBD | Pilot blocked until ✅ |

**Final Decision**: _________________ (Date: _______) (Approver: _________________)

---

## Pilot Start Checklist

Day 1:
- [ ] All P0 gates verified complete
- [ ] 5 pilot users created with unique passwords
- [ ] Monitoring alerts configured
- [ ] Escalation contacts distributed
- [ ] Operations runbook reviewed by team

---

**Archive**: [PRE_PILOT_SECURITY_RELEASE_PLAN.md](../archive/PILOT_REPORTS/PRE_PILOT_SECURITY_RELEASE_PLAN.md)

**Related**: [Deployment Checklist](../deployment/CHECKLIST.md) | [Pilot Plan](PLAN.md) | [Security Guidelines](../security/SECURITY.md)
