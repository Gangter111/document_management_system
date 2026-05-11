# RBAC Review

Generated: 2026-05-11

## Scope

Reviewed role-based access control for:

- JWT role claim generation and reading
- controller-level authentication attributes
- manual role checks
- document department/assignment scoping
- backup, audit log, report, dashboard, catalog, and system endpoints
- RBAC test coverage

## Role Model

Seeded application roles:

- Admin
- Manager
- Publisher
- Staff

JWTs include both `ClaimTypes.Role` and `role`. Runtime role checks read `ClaimTypes.Role` first, then `role`, and compare case-insensitively.

## Effective Permission Matrix

| Area | Admin | Manager | Publisher | Staff |
| --- | --- | --- | --- | --- |
| Login | Yes | Yes | Yes | Yes |
| Change own password | Yes, with current password | Yes, with current password | Yes, with current password | Yes, with current password |
| Reset another user's password | Yes | No | No | No |
| Document list/detail | All active documents | Own department or assigned | Own department or assigned | Own department or assigned |
| Document create | Yes | Yes | Yes | Yes |
| Document update/archive/restore archive | Yes | Own department only | Own department only | No |
| Move document to another department | Yes | No | No | No |
| Document delete | Yes | No | No | No |
| PDF extraction | Yes | Yes | Yes | Yes |
| Dashboard | All readable documents | Own department or assigned | Own department or assigned | Own department or assigned |
| Reports | Yes | Yes | Yes | No |
| Catalog read | Yes | Yes | Yes | Yes |
| Catalog write | Yes | Yes | No | No |
| System user/role administration | Yes | No | No | No |
| Audit log read | Yes | No | No | No |
| Backup download/restore/health | Yes | No | No | No |

## Finding 1: Dashboard Leaked Cross-Department Metadata

Severity: High

Status: Fixed in this pass

Affected file:

- `DocumentManagement.Api/Controllers/DashboardController.cs`

Root cause:

- `DocumentsController` enforced document read scoping, but `DashboardController` built summaries, charts, and recent document titles from all active documents for any authenticated user.

Exploit scenario:

- A Staff user from one department could call `GET /api/dashboard` and infer document counts, departments, issue activity, and recent document titles from unrelated departments.

Mitigation applied:

- Dashboard documents are now filtered using the same read rule as document list/detail:
  - Admin sees all active documents.
  - Non-admin sees documents assigned to their username.
  - Non-admin sees documents whose `ProcessingDepartment` matches their department claim.

Regression risk:

- Dashboard numbers for Manager, Publisher, and Staff will decrease because they now reflect only readable documents. This is expected.

Test coverage:

- Added a dashboard controller regression test proving Staff sees own-department and assigned documents, but not unrelated departments.

## Finding 2: Authorization Logic Is Repeated Manually

Severity: Medium

Status: Open

Affected files:

- `DocumentManagement.Api/Controllers/DocumentsController.cs`
- `DocumentManagement.Api/Controllers/CatalogController.cs`
- `DocumentManagement.Api/Controllers/SystemController.cs`
- `DocumentManagement.Api/Controllers/ReportsController.cs`
- `DocumentManagement.Api/Controllers/BackupController.cs`
- `DocumentManagement.Api/Controllers/AuditLogsController.cs`

Root cause:

- RBAC is implemented with repeated controller helper methods instead of centralized authorization policies.

Exploit scenario:

- Future endpoints can accidentally omit or drift from the intended role checks, as happened with dashboard read scoping.

Mitigation:

- Keep current manual checks for pilot stability.
- Before broader release, introduce named policies such as `AdminOnly`, `CatalogWrite`, `ReportRead`, `DocumentWrite`, and a shared document-scope service.

## Finding 3: PermissionService Is Not Enforced By API Controllers

Severity: Medium

Status: Open

Affected files:

- `DocumentManagement.Application/Security/PermissionService.cs`
- `DocumentManagement.Api/Controllers/*.cs`

Root cause:

- `PermissionService` defines permission codes, but API controllers enforce separate hard-coded role checks.

Risk:

- The permission table and service can diverge from API behavior. Example: permission definitions suggest Staff has document edit permission, while `DocumentsController` denies Staff document update.

Mitigation:

- Treat controller checks as authoritative for pilot.
- Align or remove stale permission definitions in a later cleanup, after confirming product rules.

## Finding 4: Document Search TotalCount Can Reveal Unscoped Counts

Severity: Medium

Status: Open

Affected file:

- `DocumentManagement.Api/Controllers/DocumentsController.cs`

Root cause:

- Search items are filtered by document read scope after repository pagination, but `TotalCount` comes from the repository before RBAC filtering.

Exploit scenario:

- A non-admin user may infer the existence or volume of documents outside their department through `TotalCount`, even when those documents are not returned.

Mitigation:

- Move read-scope filtering into the repository query or add a scoped search path that calculates `TotalCount` after applying RBAC filters.
- Avoid fixing this with controller-only count truncation because it would make pagination inconsistent.

## Finding 5: Report Scope Needs Product Confirmation

Severity: Low to Medium

Status: Open

Affected file:

- `DocumentManagement.Api/Controllers/ReportsController.cs`

Root cause:

- Reports are intentionally available to Admin, Manager, and Publisher and aggregate all active documents.

Risk:

- If Managers or Publishers should only see department-scoped reporting, current behavior is too broad.

Mitigation:

- Confirm business rule. If reports should match document read scope, apply the same document scope used by dashboard and document reads.

## Verification

Build:

```powershell
dotnet build .\DocumentManagement.sln --no-restore
```

Result:

- Passed
- Warnings: 0
- Errors: 0

Tests:

```powershell
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj
```

Result:

- Passed: 44
- Failed: 0
- Skipped: 0
- Total: 44

## Recommendation

Pilot readiness after this RBAC pass is improved. The dashboard metadata leak was the most important discovered authorization gap and has been fixed.

Before go-live beyond pilot, prioritize:

1. Move document read scope into query/repository level so `TotalCount` and pagination are RBAC-correct.
2. Replace repeated controller checks with named authorization policies.
3. Reconcile `PermissionService` with actual API behavior.
4. Confirm whether reports should be global or department-scoped for Manager and Publisher.
