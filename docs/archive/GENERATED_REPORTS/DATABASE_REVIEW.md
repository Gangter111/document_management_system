# Database Review

Generated: 2026-05-11

## Active Database Design

The runtime database layer is direct SQL through:

- `IDbConnectionFactory`
- `IDatabaseDialect`
- `DocumentManagement.Infrastructure.Repositories`
- `DatabaseMigrator` for SQLite
- `SqlServerDatabaseMigrator` for SQL Server

An EF Core `AppDbContext` exists but appears stale and inactive in runtime DI.

## Schema Coverage

Core tables:

- `documents`
- `document_categories`
- `document_statuses`
- `document_history`
- `audit_logs`
- `Users`
- `Roles`
- `document_attachments`

SQLite and SQL Server migrators create broadly similar tables, but there are drift risks:

- SQLite stores date/time fields as `TEXT`.
- SQL Server also stores many date/time fields as `nvarchar(50)` instead of `date`/`datetime2`.
- Foreign keys are not declared between documents, categories, statuses, attachments, users, and roles.
- No uniqueness constraint exists for `document_number`.

## Index Review

SQLite indexes observed:

- `documents(is_active)`
- `documents(document_number)`
- `documents(title)`
- `documents(sender_name)`
- `documents(signer_name)`
- `documents(issue_date)`
- `documents(status_id)`
- `documents(urgency_level)`
- `documents(is_active, status_id, issue_date)`
- `documents(updated_at)`
- `document_categories(name)`
- `document_categories(is_active)`
- `document_statuses(code)`
- `document_statuses(is_active)`
- `audit_logs(entity_name, entity_id)`
- `audit_logs(created_at)`
- `Users(Username)`
- `Users(RoleId)`

SQL Server indexes observed:

- `documents(document_number)`
- `documents(title)`
- `documents(sender_name)`
- `documents(status_id)`
- `document_categories(name)`
- `document_statuses(code)`
- `audit_logs(entity_name, entity_id)`

Missing or weaker SQL Server indexes:

- `documents(is_active)`
- `documents(issue_date)`
- `documents(urgency_level)`
- `documents(updated_at)`
- composite `documents(is_active, status_id, issue_date)`
- `audit_logs(created_at)`
- `Users(RoleId)`
- attachment lookup index on `document_attachments(document_id)`

## Query Performance

Good:

- Search uses `COUNT(*)` and paged retrieval.
- User input in reviewed queries is parameterized.
- Several lookup and dashboard repository methods use targeted SQL.

Issues:

- `DocumentsController.GetAll`, `DashboardController`, and `ReportsController` load all active documents into memory.
- `DocumentRepository.GetAllAsync`, `GetByIdAsync`, `SearchPagedAsync`, and `AttachmentRepository.GetByDocumentIdAsync` use `SELECT *`.
- `LIKE '%keyword%'` patterns cannot use normal indexes efficiently.
- Date filtering is string-based; correctness depends on all stored values using sortable formats.
- `AuditLogRepository.GetByEntityAsync` is unpaged.

## Transaction Handling

High-risk missing transactions:

- `DocumentService.CreateAsync` creates the document, then inserts history and audit logs separately.
- `DocumentService.UpdateAsync` updates the document, then inserts history and audit logs separately.
- `DocumentService.SoftDeleteAsync` soft-deletes, then inserts history and audit logs separately.
- Backup restore copies the database file after separate validation/safety backup steps.

Impact: partial success can leave documents without audit/history rows or vice versa.

Recommendation: introduce unit-of-work/transaction support around document mutation + history + audit operations. Keep it narrow; do not rewrite the architecture aggressively.

## N+1 Query Review

No obvious classic N+1 loops were found in the active controllers/repositories. The larger risk is the opposite pattern: full-table loads followed by in-memory aggregation.

## Data Integrity Risks

| Severity | Finding | Impact | Recommendation |
| --- | --- | --- | --- |
| High | No foreign keys | Orphaned categories/statuses/attachments/users possible | Add FK constraints in a staged migration after data cleanup. |
| High | No transaction around document mutation + audit/history | Partial writes on failure | Add transaction support for document write workflows. |
| High | Default seed users with known passwords | Database compromise or default login risk | Move seed credentials to secure config and force rotation. |
| Medium | Date fields stored as text | Bad sorting/filtering and locale parsing issues | Migrate to `date`/`datetime2` for SQL Server and normalized ISO text for SQLite. |
| Medium | No document number uniqueness | Duplicate official document numbers possible | Add business-specific unique or filtered unique index if required. |
| Medium | SQL Server index set is weaker than SQLite | Production SQL Server may perform worse than pilot SQLite | Align SQL Server indexes with SQLite migrator. |
| Medium | Attachments lack `document_id` index | Attachment lookup slows as table grows | Add index on `document_attachments(document_id)`. |
| Medium | `SELECT *` in repositories | Larger payloads and schema-coupling | Select only required columns. |
| Low | Stale EF `AppDbContext` exists with old namespace | Maintenance confusion | Remove or quarantine after confirming no dependency. |

## Database Pilot Gate

Before pilot:

1. Decide SQLite vs SQL Server for pilot and validate only that provider's operational path.
2. Rotate/remove default seed credentials.
3. Add attachment and missing SQL Server indexes.
4. Cap API page sizes to prevent large scans.
5. Back up the pilot database before any restore test.

After pilot stabilization:

1. Add transactions for document write workflows.
2. Add FK constraints and data cleanup scripts.
3. Normalize date/time storage.
4. Add concurrency/version columns for document updates.

