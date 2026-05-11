# Risk Matrix

**Last Updated**: 2026-05-11 | **Status**: Pilot Phase

## Overview

This matrix tracks known risks, their likelihood and impact, and current mitigation status. Risks are prioritized by severity (likelihood × impact).

---

## Critical & High-Severity Risks

| ID | Risk | L† | I† | S‡ | Status | Owner | Mitigation / Gate |
|----|----|---|----|---|--------|-------|-------------------|
| **R-001** | Checked-in JWT/admin/default credentials reused in pilot | M | C | 🔴 | Open | DevOps/Sec | P0 gate: Rotate secrets, unique Jwt__Secret per env |
| **R-002** | Password change does not verify old password | M | H | 🟠 | ✅ Fixed | Backend | Hardened: old password now required |
| **R-003** | Upload endpoints accept files based on extension only | M | H | 🟠 | ✅ Fixed | Backend/Sec | Hardened: MIME + signature validation added |
| **R-004** | Backup restore can overwrite live DB with weak validation | L | C | 🟠 | ✅ Fixed | Backend/Ops | Hardened: SQLite header check, default-disabled in Prod |
| **R-006** | API may run over plaintext HTTP in pilot | M | H | 🟠 | Ops Control | DevOps | P0 gate: Enforce HTTPS or reverse proxy TLS |

---

## Medium-Severity Risks

| ID | Risk | L† | I† | S‡ | Status | Owner | Mitigation / Next Step |
|----|----|---|----|---|--------|-------|----------------------|
| **R-005** | Destructive user/catalog/backup actions lack complete audit | M | H | 🟡 | ✅ Fixed | Backend | Hardened: All admin mutations now logged |
| **R-007** | Dashboard/reports load all documents into memory | H | M | 🟡 | ✅ Fixed | Backend | Hardened: Aggregation moved to SQL queries |
| **R-008** | No transaction around document mutation + history + audit | M | M | 🟡 | Open | Backend/Data | P2: Add transactional consistency |
| **R-009** | No optimistic concurrency for document updates | M | M | 🟡 | Open | Backend/API | P2: Add concurrency tokens |
| **R-010** | SQL Server indexes lag SQLite indexes | M | M | 🟡 | Open | Data | P1: Align index definitions before SQL Server rollout |
| **R-011** | Duplicate source tree causes wrong-tree patch/deploy mistakes | M | M | 🟡 | Open | Release | P1: Remove duplicate tree or clarify purpose |
| **R-012** | Minimal monitoring and alerting | H | M | 🟡 | Open | DevOps | P1: Implement health checks and alerts |

---

## Low-Severity Risks (Acceptable for Pilot)

| ID | Risk | L† | I† | S‡ | Status | Owner | Notes |
|----|----|---|----|---|--------|-------|-------|
| **R-013** | Login has no rate limiting | M | M | 🟢 | Open | Backend | P1: Implement rate limiting + lockout |
| **R-014** | HTTPS metadata relaxed in non-Production | M | L | 🟢 | ✅ OK | DevOps | Intentional: enforced in Prod |
| **R-015** | Token revocation not implemented | M | L | 🟢 | ✅ OK for Pilot | Backend | Acceptable: 60-min token lifetime; P2 for Prod |
| **R-016** | Manual authorization logic in controllers | M | L | 🟢 | Open | Backend | P2: Refactor to centralized policies |
| **R-017** | PermissionService diverges from API | L | L | 🟢 | Open | Backend | P3: Align in future cleanup |

---

## Risk Status Legend

| Status | Meaning | Action |
|--------|---------|--------|
| Open | Issue exists, mitigation needed | Prioritize based on severity |
| ✅ Fixed | Issue hardened in code | Verify in testing |
| Ops Control | Issue mitigated operationally | Ensure procedure is followed |
| P0 gate | Required before pilot | Complete before deployment |
| P1 | Required before broader rollout | Schedule after pilot validation |
| P2 | Recommended for production | Plan for next sprint |
| P3 | Long-term improvement | Backlog |

---

## Severity Mapping

- **🔴 Critical** (L: any, I: Critical) - Blocks pilot or requires immediate mitigation
- **🟠 High** (L: Medium+, I: High) - Requires hardening or operational control
- **🟡 Medium** (L: Medium+, I: Medium) - Plan remediation post-pilot
- **🟢 Low** (L: any, I: Low) - Acceptable for pilot or long-term backlog

---

## Pre-Pilot Verification

**P0 Gates** (Required before pilot deployment):

- [ ] **R-001**: Unique `Jwt__Secret` generated; no placeholder secrets in Production
- [ ] **R-001**: All default seed passwords rotated; unused accounts disabled
- [ ] **R-006**: HTTPS enforced or reverse proxy TLS topology documented

**Verify in Testing**:

- [ ] **R-002**: Password change requires old password
- [ ] **R-003**: PDF upload rejects mislabeled/malicious files
- [ ] **R-004**: Backup restore validates SQLite header; disabled in Production
- [ ] **R-005**: All user/catalog/backup mutations logged

**Operational Control**:

- [ ] **R-012**: Daily backup verification procedure in place
- [ ] **R-012**: Daily security log review procedure in place
- [ ] **R-012**: Clear rollback procedure documented

---

## Ongoing Monitoring (During Pilot)

Track these risks during pilot:

1. **R-001**: No unauthorized access attempts; no default credentials used
2. **R-002, R-003**: No upload/password anomalies in audit logs
3. **R-004**: Backup procedures complete successfully daily
4. **R-005**: All destructive operations appear in audit trail
5. **R-006**: All traffic is HTTPS or reverse-proxy validated
6. **R-007**: Dashboard/reports respond within acceptable latency
7. **R-008, R-009**: No duplicate or concurrent modification issues
8. **R-010**: SQL Server performance acceptable if migrated
9. **R-011**: No patch/deploy confusion; deploy from correct tree only
10. **R-012**: No unmonitored outages; alerting functional

---

## Post-Pilot Risk Review

After pilot (recommended timeline):

| Sprint | Priority | Risks | Owner |
|--------|----------|-------|-------|
| **Immediate** (P0) | Review and validate all P0 gates remain active | R-001, R-006 | DevOps/Sec |
| **Pilot+1** (P1) | Plan P1 hardening | R-010, R-011, R-012, R-013, R-015 | Backend/Data |
| **Pilot+3** (P2) | Plan optional hardening | R-008, R-009, R-014, R-016 | Backend/Arch |
| **Backlog** (P3) | Long-term | R-017 | Backend |

---

**Archive**: [RISK_MATRIX.md](../archive/GENERATED_REPORTS/RISK_MATRIX.md)  
**Related**: [Security Baseline](SECURITY.md) | [RBAC](RBAC.md)
