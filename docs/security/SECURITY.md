# Security Baseline Assessment

**Generated**: 2026-05-11 | **Status**: Pilot-Ready with Gates

## Executive Summary

The API has baseline security hardening suitable for a controlled internal pilot deployment only. JWT validation is implemented, core upload protections and backup safety measures are in place, and authorization is role-based. However, several higher-risk issues remain unmitigated and require operational controls or future code hardening.

**Pilot Readiness**: Conditional (depends on completing P0 security gates)  
**Production Readiness**: No (requires additional hardening and external secret management)

---

## Critical Findings

### 1. Source-Controlled JWT Secrets and Default Credentials

**Severity**: 🔴 CRITICAL  
**Status**: Requires Operational Control

- JWT `Secret` is a placeholder in source control
- Default seed passwords exist: `admin123`, `manager123`, `publisher123`, `staff123`
- Production startup blocks placeholder JWT; Development/Staging need operational control

**Mitigation**:
- [ ] Generate cryptographically random `Jwt__Secret` for each environment
- [ ] Supply secrets via environment variables (not checked-in config)
- [ ] Rotate all default user passwords before pilot
- [ ] Audit that no seed accounts remain active in pilot

**Owner**: DevOps/Operations  
**Deadline**: Before pilot deployment (P0 gate)

### 2. Backup Restore Can Overwrite Live Database

**Severity**: 🔴 CRITICAL  
**Status**: Mitigated (API restore default-disabled in Production)

- API `/api/backup/restore` can be called by admin to upload and activate a database file
- Weak validation existed; now improved with SQLite header check
- Production defaults to restore-disabled; must be explicitly enabled

**Current Controls**:
- ✅ SQLite header validation added
- ✅ Restore default-disabled in Production (`Backup:AllowApiRestore=false`)
- ✅ Audit logging for restore operations
- ⚠️ Schema validation still limited

**Recommendation**:
- Keep API restore disabled in Production
- Use offline restore scripts for production data recovery
- If API restore needed, require out-of-band confirmation (maintenance window)

---

## High-Risk Findings

### 3. PDF Upload Validation

**Severity**: 🟠 HIGH  
**Status**: Mitigated (validation hardened)

- PDF extraction now validates content type and file signature (`%PDF-`)
- Malicious or mislabeled files are rejected before parsing

**Controls Implemented**:
- ✅ Content type whitelist: `application/pdf`, `application/octet-stream`
- ✅ File signature validation (`%PDF-` header check)
- ✅ Size limit recommended (operational control)

**Remaining Risks**:
- PDF parser vulnerabilities not sandboxed
- Recommendation: Consider PDF parsing sandboxing for production

### 4. Password Change Does Not Require Old Password Verification

**Severity**: 🟠 HIGH  
**Status**: Mitigated (hardened in code)

- Self-service password changes now require `OldPassword` verification
- Admin password reset separated into admin-only endpoint

**Controls Implemented**:
- ✅ Old password verification required for self-service changes
- ✅ Audit logging for all password changes
- ✅ Separate admin-only password reset endpoint

### 5. Authorization Leaks in Search/Reports/Dashboard

**Severity**: 🟠 HIGH  
**Status**: Mitigated (scoping added)

- Dashboard, Reports, and Search now apply same RBAC scoping as document reads
- Non-admin users can no longer infer metadata about unrelated departments

**Controls Implemented**:
- ✅ Dashboard filters documents by read permission
- ✅ Reports scoped to readable documents
- ✅ Search TotalCount now scoped (no more leakage of hidden document counts)

---

## Medium-Risk Findings

### 6. No Login Rate Limiting

**Severity**: 🟡 MEDIUM  
**Status**: Open (not implemented)

- Login endpoint has no application-level rate limiting
- Brute-force attacks against password are theoretically possible
- Default accounts should not exist (see Critical finding #1)

**Mitigation**:
- [ ] Implement rate limiting: max 5 failed attempts per username per minute
- [ ] Implement progressive backoff or account lockout after repeated failures
- [ ] Log all failed login attempts (without passwords)

**Owner**: Backend  
**Priority**: P1 (before broader production rollout)

### 7. HTTPS Metadata Not Enforced Outside Production

**Severity**: 🟡 MEDIUM  
**Status**: Mitigated (enforced in Production, relaxed in Dev/Staging)

- `RequireHttpsMetadata` is `false` in Development and Staging
- `RequireHttpsMetadata` is `true` in Production
- Allows development over HTTP; prevents accidental production HTTP deployment

**Current Configuration**:
- ✅ Production enforces HTTPS metadata validation
- ✅ Non-Production allows HTTP for development convenience

**Operational Requirement**:
- All Production deployments must use HTTPS or reverse proxy with TLS termination
- Document the exact topology (e.g., IIS front-end → internal HTTP backend)

### 8. Active-User Middleware Has No Token Version Check

**Severity**: 🟡 MEDIUM  
**Status**: Open (acceptable for pilot, recommended for production)

- Deactivated users are blocked on each request
- Password changes do not invalidate issued tokens until expiration
- Token lifetime (default: 60 minutes) provides practical revocation delay

**Acceptable for Pilot**: Yes (token lifetime is short enough)

**Recommended for Production**:
- [ ] Add security stamp/token version to JWT claims
- [ ] Compare security stamp on each authenticated request
- [ ] Invalidate all tokens when user is deactivated or password is reset

### 9. PDF Upload Written to Disk Before Content Validation

**Severity**: 🟡 MEDIUM  
**Status**: Mitigated (validation improved)

- File is written to temp before content validation begins
- Content validation now happens before parsing

**Recommendation**:
- Validate file header from request stream before writing full file to disk
- Use a bounded temp directory with cleanup policy

---

## Lower-Risk Findings

### 10. RBAC Implementation Is Manual and Repetitive

**Severity**: 🟢 LOW  
**Status**: Open (acceptable for pilot, recommend refactoring for production)

- Authorization logic repeated across controllers
- Risk of future drift or omissions

**Recommendation**:
- Consolidate into centralized authorization policies:
  - `AdminOnly` policy
  - `CatalogWrite` policy
  - `DocumentScope` service for document-level checks
- Keep current manual checks for pilot stability

### 11. PermissionService Definitions Diverge from API Behavior

**Severity**: 🟢 LOW  
**Status**: Open (treat controller checks as authoritative)

- `PermissionService` exists but controllers enforce different rules
- Example: Permission table suggests Staff can edit; controller denies Staff edit

**Recommendation**:
- Treat current API controller checks as authoritative for pilot
- Align or remove stale permission definitions in future cleanup

---

## Hardening Summary

| Issue | Original Risk | Current Status | Pilot Impact |
|-------|----------------|----------------|--------------|
| JWT placeholder secrets | Critical | Requires ops control | P0 gate |
| Backup restore safety | Critical | Default-disabled | Operational control required |
| PDF upload validation | High | Hardened | ✅ Pilot-ready |
| Password change validation | High | Hardened | ✅ Pilot-ready |
| Authorization scoping | High | Hardened | ✅ Pilot-ready |
| Login rate limiting | High | Open | Acceptable for small pilot |
| HTTPS enforcement | Medium | Enforced in Prod | ✅ Prod config ready |
| Token revocation | Medium | Open | Acceptable (short token lifetime) |
| Manual RBAC | Medium | Open | Acceptable for pilot |

---

## Pre-Pilot Checklist

Before pilot deployment, verify:

- [ ] **Secrets Management**
  - [ ] Unique `Jwt__Secret` generated for each environment
  - [ ] All default seed passwords rotated
  - [ ] Secrets supplied via environment/service settings (not source control)

- [ ] **Backup & Restore**
  - [ ] Backup job runs successfully
  - [ ] Restore tested on isolated database copy
  - [ ] Rollback procedure documented and practiced

- [ ] **HTTPS**
  - [ ] HTTPS enforced or reverse proxy with TLS documented
  - [ ] Certificate valid and properly configured

- [ ] **Audit Logging**
  - [ ] Audit log directory writable
  - [ ] Daily log review process defined
  - [ ] Alerting for security events (future: implement monitoring)

- [ ] **Account Management**
  - [ ] All pilot users have unique credentials (not defaults)
  - [ ] All unused seed accounts disabled
  - [ ] Admin account has unique strong password

---

## Pilot Operational Requirements

During pilot deployment, maintain:

- **Daily backup verification**: Run and verify backup completes successfully
- **Daily security log review**: Check for unexpected authentication failures or access denials
- **Immediate rollback capability**: Keep previous release available for fast rollback
- **Clear escalation path**: Define who to contact for security incidents

---

## Next Steps for Production Readiness

After pilot, address before broad production:

1. **P1 Gates** (required for broader internal rollout)
   - Implement login rate limiting and account lockout
   - Add comprehensive audit trail for all admin operations
   - Implement token revocation/security stamp
   - Document and enforce TLS topology

2. **P2 Hardening** (recommended for external or higher-security deployment)
   - Implement centralized authorization policies
   - Add PDF parsing sandboxing
   - Implement external secret management (e.g., Azure Key Vault)
   - Add comprehensive monitoring and alerting
   - Conduct penetration testing

3. **P3 Long-Term**
   - Consider Web Application Firewall (WAF)
   - Implement DDoS protection
   - Regular security training for operations team

---

**Last Updated**: 2026-05-11  
**Next Review**: After pilot completion  
**Archive**: [SECURITY_AUDIT.md](../archive/GENERATED_REPORTS/SECURITY_AUDIT.md)
