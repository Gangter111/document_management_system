# Test Report

Generated: 2026-05-11

## Scope

Generated integration tests for all API controllers.

No production code was changed.

## Integration Coverage Added

Added:

- `DocumentManagement.Tests/Api/ControllerSurfaceIntegrationTests.cs`

Controller coverage:

| Controller | Covered integration behavior |
| --- | --- |
| `AuthController` | Login and authenticated self password change flow. |
| `DocumentsController` | Create, get by id, list, search, update, archive, restore from archive, invalid PDF extraction upload, delete. |
| `BackupController` | Admin backup health, staff forbidden backup health, invalid restore upload rejection. |
| `CatalogController` | Read categories, staff forbidden write, category create/update/delete, status create/update/delete. |
| `DashboardController` | Authenticated dashboard read. |
| `LookupsController` | Category and status lookup reads. |
| `ReportsController` | Staff forbidden report access, Publisher report access. |
| `SystemController` | Staff forbidden roles access, admin roles/search/create/update/delete user flow. |
| `AuditLogsController` | Admin document audit log read, staff forbidden audit log read. |

## Existing Coverage Preserved

Existing integration and unit tests still cover:

- Document permission flow across Staff, Publisher, Manager, and Admin.
- Audit log creation for document create/update/delete.
- Health endpoint.
- Invalid login.
- Protected endpoint unauthenticated/unauthorized responses.
- Deactivated-user token rejection.
- Publisher department-scope enforcement.
- Document service audit behavior.
- Permission service role matrix.
- Backup controller unit guardrails.

## Verification

Command:

```powershell
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj
```

Result: Passed.

Summary:

- Total: 43
- Passed: 43
- Failed: 0
- Skipped: 0

## Remaining Useful Test Gaps

- Real successful backup download and restore round-trip should be tested in an isolated temp content root.
- PDF extraction success path needs a controlled valid PDF fixture.
- Authorization should be expanded once document read scoping is implemented.
- Production startup configuration tests should validate environment-secret behavior.

