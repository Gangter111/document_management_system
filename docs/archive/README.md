# Archive

This directory contains historical audit reports, generated assessments, and reference materials from the development and pilot phases. These documents are preserved for:

- **Compliance & Audit Trail** - Historical record of system reviews
- **Reference** - Understanding evolution of requirements and decisions
- **Post-Pilot Learning** - Pilot results and lessons learned
- **Traceability** - Connection between assessments and implemented fixes

## Contents

### GENERATED_REPORTS/

Comprehensive audit and review reports generated during development freeze and pre-pilot assessment phases.

| Report | Purpose | Generated |
|--------|---------|-----------|
| ARCHITECTURE_REPORT.md | System architecture analysis | 2026-05-11 |
| API_REVIEW.md | API surface and design review | 2026-05-11 |
| DATABASE_REVIEW.md | Database design and schema analysis | 2026-05-11 |
| RBAC_REVIEW.md | Role-based access control analysis | 2026-05-11 |
| SECURITY_AUDIT.md | Security posture assessment | 2026-05-11 |
| HARDENING_REPORT.md | Security hardening summary | 2026-05-11 |
| PERFORMANCE_REVIEW.md | Performance analysis and findings | 2026-05-11 |
| BUILD_REPORT.md | Build process validation | 2026-05-11 |
| TEST_REPORT.md | Test suite coverage analysis | 2026-05-11 |
| FINAL_AUDIT_SUMMARY.md | Executive summary of all audits | 2026-05-11 |
| STAGING_DEPLOYMENT.md | Staging deployment report | 2026-05-11 |

**Note**: Active issues and findings from these reports have been consolidated into the main documentation under `security/`, `deployment/`, and `operations/` directories. These archives are for reference only.

### PILOT_REPORTS/

Pilot phase planning, evaluation, and decision documents.

| Report | Purpose | Generated |
|--------|---------|-----------|
| PILOT_5_USERS.md | 5-user pilot execution plan | 2026-05-11 |
| FIVE_USER_TRIAL_EVALUATION.md | Pilot evaluation and decision | 2026-05-11 |
| PRE_PILOT_SECURITY_RELEASE_PLAN.md | Pre-pilot security gates and release plan | 2026-05-11 |

## Using Archived Content

### When to Reference These Documents
- **Researching a specific finding** - Original audit reports provide full context
- **Understanding deployment history** - Staging and deployment reports show evolution
- **Pilot feedback tracking** - Pilot reports document decisions and conditions

### What Should Be Updated Instead
If you need to:
- **Update deployment steps** → Use `deployment/CHECKLIST.md` (live)
- **Report new risks** → Update `security/RISK_MATRIX.md` (live)
- **Document operational issues** → Update `operations/OPERATIONAL_GUIDE.md` (live)
- **Record security findings** → Update `security/SECURITY.md` (live)

## Retention Policy

- **GENERATED_REPORTS/** - Retained for 12+ months for compliance; reviewed annually
- **PILOT_REPORTS/** - Retained indefinitely as part of go-live decision trail

---

**Archive Created**: 2026-05-11
**Purpose**: Preservation of audit trail and historical reference
