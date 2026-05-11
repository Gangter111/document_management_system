# 5-User Pilot Plan

**Scope**: Controlled internal pilot with 5 named users  
**Duration**: 5 business days  
**Target**: Validate system readiness for broader rollout

---

## Pilot Objectives

1. **Functional Validation** - All core workflows work correctly
2. **Performance Baseline** - Establish expected response times
3. **Operational Readiness** - Operators can run the system
4. **Security Validation** - No unexpected access/data issues
5. **User Feedback** - Gather input for improvements

---

## Pilot User Mix

Use **real named accounts**, not default seeded passwords.

Recommended 5-person team:

| User | Role | Department | Purpose |
|------|------|-----------|---------|
| **PilotAdmin** | Admin | IT | User management, backup verification, system administration |
| **PilotMgr** | Manager | Finance | Document approvals, department workflows, reporting |
| **PilotPub** | Publisher | IT | Document publication, metadata management |
| **PilotStaff1** | Staff | Finance | Create documents, verify department scoping |
| **PilotStaff2** | Staff | HR | Cross-user workflows, document assignment verification |

---

## Pre-Pilot Requirements

**All P0 gates must be completed** (see [Deployment Checklist](../deployment/CHECKLIST.md)):

- [ ] Unique JWT secret deployed (no placeholders)
- [ ] Default passwords rotated
- [ ] HTTPS or internal-LAN exception documented
- [ ] Backup tested and working
- [ ] Restore procedure tested on separate environment
- [ ] Monitoring and alerting ready
- [ ] Rollback procedure tested
- [ ] Operations team trained

---

## Pilot Workflow

### Day 1: Onboarding & Basic Workflows

**Admin (PilotAdmin)**:
- [ ] Confirm API health check passes
- [ ] Confirm Windows Service running
- [ ] Confirm daily backup scheduled/working
- [ ] Verify all 5 pilot users created with correct roles/departments

**All Users**:
- [ ] Login to WPF application
- [ ] Change password to unique strong password
- [ ] Verify dashboard loads
- [ ] Verify basic API connectivity

**Staff (PilotStaff1, PilotStaff2)**:
- [ ] Create document (draft status)
- [ ] View document details
- [ ] Attach PDF file

### Day 2: Department Scoping & Workflows

**PilotMgr (Manager)**:
- [ ] View only Finance department documents
- [ ] Cannot see HR documents
- [ ] Generate department report

**PilotStaff1 (Staff - Finance)**:
- [ ] Create documents visible to Finance only
- [ ] Verify Staff from HR cannot see Finance documents

**PilotStaff2 (Staff - HR)**:
- [ ] Create HR documents
- [ ] Verify visibility scoping works

### Day 3: Advanced Workflows & Approval Process

**PilotPub (Publisher)**:
- [ ] Publish/update document metadata
- [ ] Cannot delete documents
- [ ] Approve for distribution

**PilotMgr**:
- [ ] Review pending documents
- [ ] Approve or return for changes

**All Users**:
- [ ] Verify document lifecycle state changes
- [ ] Verify notifications/updates working

### Day 4: Admin Operations & Security

**PilotAdmin**:
- [ ] User management (create, disable, reset password)
- [ ] View audit logs (all operations appear)
- [ ] Download backup
- [ ] Review system health

**All Users**:
- [ ] Verify audit log entries for their operations
- [ ] Verify no unauthorized visibility

### Day 5: Stability & Rollback Testing

**Admin**:
- [ ] Test restore procedure on staging (not production)
- [ ] Verify no data loss
- [ ] Confirm monitoring alerts working

**All Users**:
- [ ] Continue normal workflows
- [ ] Report any issues

---

## Success Criteria

### Go/No-Go Decision

**GO for broader rollout if**:
- ✅ All P0 gates remain active
- ✅ All core workflows completed without critical issues
- ✅ Backup/restore procedure verified
- ✅ Performance acceptable
- ✅ No unauthorized data access detected
- ✅ Pilot users comfortable with system
- ✅ Operations team confident in support

**NO-GO if**:
- ❌ P0 security gate broken (e.g., default password active)
- ❌ Critical bug discovered (data loss, corruption, unauthorized access)
- ❌ Performance severely degraded (response times > 10s)
- ❌ Backup/restore fails
- ❌ Monitoring not operational

---

## Issue Tracking

Record all issues with:
- **Reporter**: Name + role
- **Component**: Which feature/endpoint
- **Severity**: P0 (blocks use), P1 (workaround exists), P2 (cosmetic)
- **Reproducibility**: Always, sometimes, once
- **Expected**: What should happen
- **Actual**: What happened instead
- **Timestamp**: When detected
- **Logs**: Error messages/stack traces

---

## Daily Monitoring During Pilot

**Operations team must verify daily**:

- ✅ Windows Service running
- ✅ `/health` endpoint responds
- ✅ Latest backup exists and is recent
- ✅ No ERROR/FATAL in logs
- ✅ Disk free space > 20%
- ✅ All 5 users able to login

---

## Post-Pilot Review

After 5 business days:

1. **Collect feedback** from all 5 pilot users
2. **Review logs** for errors/warnings
3. **Analyze metrics**: Backup success, performance, uptime
4. **Decision meeting** - GO or address blockers

---

## Escalation

Contact technical lead if:
- API won't start
- Database corruption suspected
- Unusual performance degradation
- Security incident (unauthorized access)
- Backup/restore failure

---

**Archive**: [PILOT_5_USERS.md](../archive/PILOT_REPORTS/PILOT_5_USERS.md) | [FIVE_USER_TRIAL_EVALUATION.md](../archive/PILOT_REPORTS/FIVE_USER_TRIAL_EVALUATION.md)

**Related**: [Readiness Assessment](../deployment/READINESS.md) | [Deployment Checklist](../deployment/CHECKLIST.md) | [Security Plan](SECURITY_PLAN.md)
