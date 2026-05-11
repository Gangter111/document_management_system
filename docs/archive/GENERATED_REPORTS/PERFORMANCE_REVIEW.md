# Performance Review

Generated: 2026-05-11

## Summary

The API is acceptable for a small pilot dataset, but several endpoints will degrade as document volume grows. The biggest risks are unbounded document reads, in-memory dashboard/report aggregation, synchronous startup migrations, and large file/backup operations running directly in request paths.

## Findings

| Severity | Finding | Affected files | Impact | Recommendation |
| --- | --- | --- | --- | --- |
| High | Unbounded document list endpoint | `DocumentsController.cs`, `DocumentRepository.cs` | Large payloads and memory pressure | Replace `GET /api/documents` with paged access or cap results. |
| High | Dashboard loads all active documents | `DashboardController.cs` | Slow dashboard and high memory use | Use SQL aggregate queries or existing `DashboardRepository`. |
| High | Reports load all active documents | `ReportsController.cs` | Slow reports and high memory use | Move summary/grouping to SQL queries. |
| High | Backup/restore executes in HTTP request path | `BackupController.cs` | Long requests, thread/resource contention, timeout risk | Use background job or operational script for production-sized databases. |
| Medium | `SELECT *` is used in repositories | `DocumentRepository.cs`, `AttachmentRepository.cs` | Larger I/O and coupling to schema | Select only columns needed by model/endpoint. |
| Medium | `LIKE '%keyword%'` scans | `DocumentRepository.cs`, `UserManagementRepository.cs` | Indexes cannot be used efficiently for contains search | Add full-text search or prefix search strategy for large datasets. |
| Medium | Synchronous migrator execution during startup | `DatabaseMigrator.cs`, `SqlServerDatabaseMigrator.cs`, `Program.cs` | Startup delays and blocking I/O | Run migrations as deployment step or use async where practical. |
| Medium | Per-request active-user database query | `Program.cs` | Adds DB roundtrip to every authenticated request | Cache active user status briefly or use token version/security stamp. |
| Medium | Synchronous hash computation after file copy | `LocalFileStorageService.cs` | Blocks while hashing large files | Use async stream hashing in .NET 8 or bounded background processing. |
| Medium | Date values stored and parsed as strings | Repositories/controllers | Sorting/filtering costs and parsing inconsistency | Normalize date storage and query in database-native date types. |
| Low | Request logging can become noisy under high traffic | `Program.cs` | Disk growth and logging overhead | Tune Serilog levels and retention for pilot traffic. |

## Memory Usage Risks

- `DashboardController.Get` materializes all active documents.
- `ReportsController.GetSummary` materializes all active documents and categories.
- `DocumentsController.GetAll` materializes all active documents and DTOs.
- PDF extraction reads extracted text into strings; large PDFs can increase memory pressure.
- Backup download creates a physical backup file before returning it.

## Blocking and Synchronous I/O

Observed synchronous paths:

- SQLite and SQL Server migrators use synchronous `Open`, `ExecuteNonQuery`, and `ExecuteScalar`.
- `LocalFileStorageService.ComputeSha256` uses synchronous `File.OpenRead` and `ComputeHash`.
- File deletes/copies in backup restore are synchronous.

These are acceptable for small pilot operations, but production readiness should move database migration to deployment scripts and keep heavy file work controlled.

## Large Payload Handling

Positive:

- PDF extraction has `[RequestSizeLimit(20 * 1024 * 1024)]`.
- Backup restore has `[RequestSizeLimit(200_000_000)]`.
- Backup download uses `PhysicalFile`.

Gaps:

- Document list has no payload limit.
- Audit logs are unpaged.
- Reports/dashboard can return large grouped results if data contains many departments/status text variants.

## Pilot Performance Recommendations

Must fix before broad pilot:

1. Cap document search `pageSize`.
2. Avoid using `GET /api/documents` from clients for large lists.
3. Add paging to audit logs.
4. Move dashboard/report aggregation to database queries or use `DashboardRepository`.

Should fix soon:

1. Align SQL Server indexes with SQLite.
2. Add `document_attachments(document_id)` index.
3. Cache current-user active checks for a short TTL.
4. Move backup/restore to operational scripts for production-sized databases.

Nice to have:

1. Add full-text search for document title/number/sender.
2. Add response compression if browser clients or large JSON payloads are introduced.
3. Add basic request duration metrics and slow query logging.

