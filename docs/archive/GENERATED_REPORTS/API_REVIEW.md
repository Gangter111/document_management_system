# API Review

Generated: 2026-05-11

## Controller Inventory

| Controller | Routes | Assessment |
| --- | --- | --- |
| `AuthController` | `api/auth/login`, `api/auth/change-password` | Thin for login, but uses reflection against session model and does not verify old password on change. |
| `DocumentsController` | `api/documents/*` | Fat controller; contains validation, authorization, DTO mapping, upload temp-file handling, and domain state changes. |
| `BackupController` | `api/backup/*` | Fat controller; backup/restore orchestration and SQLite validation live at HTTP boundary. |
| `DashboardController` | `api/dashboard` | Aggregates full document set in memory; no pagination/caching. |
| `ReportsController` | `api/reports/summary` | Aggregates full document set in memory; role check is manual. |
| `AuditLogsController` | `api/audit-logs` | Admin-only and basic validation; no pagination. |
| `CatalogController` | `api/catalog/*` | Direct repository access; manual role checks; no standard error shape. |
| `SystemController` | `api/system/*` | Admin-only user management; direct repository access. |
| `LookupsController` | `api/lookups/*` | Simple lookup endpoints. |

## Request Validation

Strengths:

- Basic required-field checks exist for document create/update.
- User/catalog repositories validate important fields before persistence.
- Route id mismatch is checked on document update.
- Backup restore checks file presence and extension.

Gaps:

- DTOs do not use data annotations or central validation.
- Date fields are strings, so invalid date formats can persist or produce inconsistent report behavior.
- Enum-like values (`DocumentType`, `ConfidentialityLevel`, `UrgencyLevel`, `OcrStatus`) are not constrained.
- `pageSize` is not capped; a caller can request very large pages.
- `GetAll` endpoints can return unbounded collections.
- Upload endpoints do not validate MIME type or content signature.

## Response Consistency

Observed response styles are mixed:

- Some endpoints return DTOs.
- Some return plain strings for errors.
- Some return anonymous objects.
- Some return `NoContent`.
- Unhandled exceptions return a generic JSON object from global exception middleware.

Impact: API clients must handle multiple error formats, and future integration tests become less reliable.

Recommendation: adopt a single error contract, preferably `ProblemDetails`, while preserving existing success DTOs.

## Status Codes

Positive:

- `401` is used for unauthenticated/invalid active-user states.
- `403` is used for role failures.
- `404` is used for missing documents/database.
- `201 Created` is used for document/user/catalog creation.
- `204 NoContent` is used for successful updates/deletes.

Issues:

- Expected business validation often returns raw `400` strings.
- `ChangePassword` returns `400` for several cases that could be more specific.
- `Delete` on already inactive document returns `204`, which is acceptable but should be documented as idempotent.
- `BackupController` returns filesystem paths in some responses, which may leak deployment internals.

## Exception Handling

Strengths:

- Global unhandled exception middleware returns a generic message.
- Exceptions are logged with method/path.

Issues:

- `SystemController`, `CatalogController`, and `BackupController` return `InvalidOperationException.Message` directly.
- `DocumentService.GetRequiredActiveDocumentAsync` throws for expected not-found business flow; controller callers avoid this in some paths but not all service methods.

Recommendation: use application result types or typed domain exceptions mapped centrally.

## Pagination

Endpoints with pagination:

- `GET /api/documents/search`

Endpoints needing pagination or limits:

- `GET /api/documents`
- `GET /api/audit-logs`
- `GET /api/system/users`
- `GET /api/catalog/categories?includeInactive=true`
- `GET /api/catalog/statuses?includeInactive=true`
- Dashboard/report aggregations should use bounded SQL aggregations rather than full list loads.

## Concurrency Handling

No optimistic concurrency token, row version, ETag, or `updated_at` precondition was observed.

Risk: two users can edit the same document and last write wins silently.

Recommendation:

- Add an `UpdatedAt`/version precondition to update requests.
- Return `409 Conflict` when the persisted version changed.

## Async Usage

Good:

- Controllers and repositories are mostly async.
- File upload copy uses async stream copy.

Gaps:

- Database migrators use synchronous `ExecuteNonQuery` during startup.
- `LocalFileStorageService.ComputeSha256` performs synchronous file hashing after async copy.
- Some service methods return already materialized in-memory results and then filter again in controllers.

## API Quality Findings

| Severity | Finding | Affected files | Recommendation |
| --- | --- | --- | --- |
| High | `GET /api/documents` is unbounded | `DocumentsController.cs` | Deprecate or require pagination. |
| High | `ChangePassword` ignores old password | `AuthController.cs`, `AuthService.cs` | Verify current password for self-service changes. |
| High | Upload validation is incomplete | `DocumentsController.cs`, `BackupController.cs` | Validate content type, signature, size, and schema. |
| Medium | Controllers contain business authorization logic | `DocumentsController.cs`, `CatalogController.cs`, `SystemController.cs`, `ReportsController.cs` | Move to policies/services incrementally. |
| Medium | Dashboard/reports load all documents | `DashboardController.cs`, `ReportsController.cs` | Move aggregation to repository SQL. |
| Medium | No concurrency protection | Document update endpoints | Add version/updated-at checks and return `409`. |
| Medium | Error response shape is inconsistent | Most controllers | Standardize on `ProblemDetails`. |
| Medium | Audit logs are unpaged | `AuditLogsController.cs` | Add page number/page size cap. |
| Low | DTO mapping is repeated manually | Multiple controllers | Consider small mapper helpers after stabilization. |

## Minimal API Stabilization Plan

1. Cap `pageSize` on document search.
2. Add pagination to documents list and audit logs.
3. Standardize validation errors with `ProblemDetails`.
4. Harden upload validation before parser/storage.
5. Split password change into self-service change and admin reset semantics.
6. Introduce named authorization policies for admin/catalog/report/document actions.

