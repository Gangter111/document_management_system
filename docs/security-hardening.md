# Security Hardening Runbook

This system must be deployed as enterprise software, not as a copied debug folder.

## Secrets

- Never use committed JWT or admin seed secrets in production.
- Set `Jwt:Secret` through environment variables, protected external configuration, or a managed secret store.
- Production startup refuses missing, weak, or placeholder JWT secrets.
- Keep `AdminSeed:Password` empty in production unless a controlled provisioning run needs it.

## Admin Accounts

- Default development users are seeded only in Development.
- Production administrators must be created through a controlled operational process and rotated before go-live.
- Login failures are audit logged without passwords or JWTs.

## Backup And Restore

- Backup creation is admin-only and audit logged.
- Restore requires `Maintenance:RestoreEnabled=true`.
- Normal API traffic can be blocked with `Maintenance:Mode=true`.
- Restore uploads are staged, validated as application SQLite databases, and rollback is attempted on failure.
- Prefer offline/service-controlled restore for real production incidents.

## Attachment Storage

- Attachments are stored with content-addressed names and magic-byte validation.
- User filenames are not used as storage paths.
- Reconciliation reports DB metadata without files and stored files without DB metadata.
- Orphan cleanup is opt-in and must not delete DB-referenced files.

## PDF Extraction

- Current extraction is in-process, bounded by size, page count, text size, timeout, and cancellation.
- First-generation OCR fallback is optional and runs only after normal PDF text extraction classifies a file as scanned/image-only.
- OCR uses local Tesseract CLI with Vietnamese plus English language configuration when installed; English fallback is supported.
- OCR page rendering uses bounded in-process Docnet.Core rendering with per-job temporary isolation.
- OCR work uses a unique temporary folder per extraction and deletes it after success, failure, timeout, or cancellation.
- OCR is capped by page count, DPI, rendered image size, OCR character count, and per-page timeout.
- Failures are classified as unsupported, encrypted, scanned/image-only, malformed, too large, or timeout.
- No extracted text, local file paths, passwords, or JWTs should be written to logs.
- `IPdfExtractionWorker` is the replacement point for a future process-isolated extractor.
- Each API extraction request uses a unique working folder under the OS temp path and deletes it after the job completes.
- The extraction job request carries correlation id, file token, file size, timeout, and cancellation state so an out-of-process worker can be introduced without changing API contracts.

Future isolated worker model:

- Keep the API responsible for authentication, upload validation, temporary staging, and classified API responses.
- Move PDF parsing/OCR into a separate worker executable running under a restricted service identity.
- Pass only a short-lived file token or staged file path plus correlation id to the worker.
- Return only extracted metadata/text or classified failure; never return local worker paths.

Recommended Windows Job Object limits for the future worker:

- Per-job timeout: 30 seconds for normal metadata extraction; configurable for OCR.
- Memory cap: start at 512 MB per worker process and tune from production evidence.
- CPU limit: cap worker CPU rate or process priority so hostile PDFs cannot starve the API.
- Kill the full process tree on timeout or cancellation.
- Disable network access for the worker service account unless OCR dependencies require controlled local resources.

OCR extension points:

- Keep OCR behind `IPdfExtractionWorker`; do not put OCR code in controllers.
- Treat Poppler/Ghostscript/Tesseract as untrusted document-processing dependencies and run them only in the isolated worker for higher-risk deployments.
- Keep OCR output bounded and classify image-only, encrypted, and malformed documents separately.
- Do not enable OCR on broad hostile workloads until the renderer and Tesseract are deployed under a restricted service identity and process/job limits are enforced.

## Deployment Checklist

- Publish with `tools/publish-production.ps1`.
- Code-sign API and WPF binaries.
- Package with MSI or MSIX; do not distribute raw publish folders as the final product.
- Install under a protected directory.
- Store writable data under `C:\ProgramData\QuanLyVanBan` or an approved enterprise data location.
- Lock ACLs for database, attachment, backup, and log folders.
- Configure Windows service identity with least privilege.
- Configure backup schedule and test restore on a separate machine.
- Confirm `/health` before client rollout.

## Runbook Warnings

- Do not enable restore while users are actively using the system.
- Do not keep SQLite as the primary database for broad multi-user production deployment.
- Do not turn on orphan cleanup without reviewing the reconciliation report first.
- Do not process hostile/scanned PDF workloads at scale until the extraction worker is process-isolated.

## Release Verification

- Run `tools/verify-release.ps1` before tagging a release candidate.
- Use `tools/verify-release.ps1 -RunPublish` only when `DMS_JWT_SECRET` is set to a real production-grade secret.
- The verification script does not create MSI/MSIX installers; installer creation and signing remain separate release steps.
