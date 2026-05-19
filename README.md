# QuanLyVanBan / DocumentManagement

QuanLyVanBan is an enterprise document management system with a deterministic semantic review workflow for Vietnamese administrative and corporate documents. It combines a WPF desktop client, an ASP.NET Core API, persistence services, OCR/PDF infrastructure, and a provenance-first document intelligence layer.

The intelligence layer assists reviewers. It does not autonomously approve records, self-learn from user behavior, or become the authority for business data.

## Architecture Flow

```text
WPF client
  -> ASP.NET Core API
  -> Application services
  -> Domain rules
  -> Infrastructure persistence/storage/OCR

PDF or OCR content
  -> MinerU content_list_v2 adapter
  -> DocumentStructure
  -> SemanticExtractionPipeline
  -> field candidates, confidence, rejection reasons
  -> ReviewEvidenceResolver
  -> review fields and overlay regions
  -> WPF human review workflow
```

Main projects:

* `DocumentManagement.Wpf`: WPF desktop client, MVVM screens, dashboard, document forms, and semantic review UI.
* `DocumentManagement.Api`: authentication, document endpoints, admin/backup operations, persistence endpoints, and API hosting.
* `DocumentManagement.Application`: application orchestration and service contracts.
* `DocumentManagement.Domain`: entities, document status semantics, and domain rules.
* `DocumentManagement.Infrastructure`: SQLite/SQL Server persistence, migrations, file storage, backup, demo data, OCR/PDF, and export services.
* `DocumentManagement.Intelligence`: MinerU adaptation, semantic extraction, provenance, deterministic review mapping, resolver ordering, replay/audit snapshots, and overlay navigation.
* `DocumentManagement.Contracts`: shared DTO contracts.
* `DocumentManagement.Tests`: service, security, persistence, intelligence, review determinism, runtime safety, and UI-critical regression tests.

## Deterministic Semantic Review

The semantic review path is designed around explainable evidence:

```text
PDF
-> OCR/MinerU blocks
-> normalized document structure
-> deterministic field extractors
-> candidate confidence and rejection reasoning
-> evidence provenance
-> resolver-authoritative ordering
-> human review UI with overlay navigation
```

Review evidence is classified as:

* Primary: the exact source used for the proposed value.
* Supporting: evidence that explains semantic validity, such as a signer role near a signer name.
* Contextual: nearby ordered content that helps review without becoming the value.

Rejected candidates remain visible when useful. For example, a DOB-looking date inside `sinh ngay`, `ngay sinh`, or `DOB` context is hard rejected and must not become the issue date.

## Resolver Authority

`ReviewEvidenceResolver` is the sole semantic ordering authority for review evidence. Overlay traversal, focus restoration, replay, audit, diff, diagnostics, and operational observability consume resolver-normalized order; they do not re-rank or mutate semantic evidence.

Determinism is a product requirement:

* stable evidence ordering.
* stable overlay traversal.
* stable semantic group IDs and overlay IDs.
* stable focus capture and restoration across refresh/remap cycles.
* stable replay, audit, diff, and operational snapshots.
* no timing-based correctness.
* no coordinate-only focus recovery.
* no hash-order dependent output.

Repeated header/footer evidence is demoted with diagnostics inside a resolver invocation. It is not removed, so provenance remains reviewable.

## Current Capabilities

Implemented and tested capabilities include:

* OCR/MinerU block adaptation into `DocumentStructure`.
* semantic extraction for issuer, document number, issue date, title/trich yeu, signer, recipients, and body sections.
* real-world Vietnamese administrative/corporate heuristic tuning for common noisy OCR layouts.
* deterministic rejection of DOB-as-issue-date candidates.
* document number normalization for noisy `So`, `S6`, `S0`, spaced `S o`, `No`, and `Ky hieu` markers.
* issuer promotion from guarded top-left organization evidence, including multi-line administrative headers.
* title/trich yeu promotion for common document types such as `QUYET DINH`, `THONG BAO`, `CONG VAN`, `TO TRINH`, `BAO CAO`, `KE HOACH`, and `NGHI QUYET`.
* direct `Kinh gui` addressee and footer `Noi nhan` recipient support.
* signer role/name evidence with stamp/signature overlap tolerance.
* body section reconstruction in visual reading order, including effective-date wording as body evidence.
* provenance propagation through extraction, review mapping, and overlay mapping.
* deterministic semantic grouping and overlay traversal.
* focus restoration across refresh and remap scenarios.
* deterministic replay, audit, diff, and bounded operational observability snapshots.
* runtime stale-refresh and cancellation safety.
* deterministic demo-data seeding and cleanup.
* SQLite pilot runtime and SQL Server deployment support.

## Real-World Corpus Tuning

Recent tuning is intentionally conservative and corpus-driven. The goal is better promotion of real Vietnamese administrative/corporate documents without replacing the deterministic model.

The tuning added lightweight heuristics for:

* organization/issuer headers.
* signer roles and person-like signer names.
* administrative titles and trich yeu subject lines.
* recipients and archive/footer blocks.
* document-number OCR whitespace and marker normalization.
* duplicated, noisy, malformed, or missing-accent OCR.
* repeated headers/footers and stamp-like fragments.

The system still prefers an empty or low-confidence value over an unsupported authoritative promotion.

## Review Examples And Artifacts

Committed deterministic artifacts:

* `artifacts/samples/decision_content_list_v2.json`: sample MinerU-style OCR fixture used by intelligence and review tests.
* `artifacts/screenshots/*.png`: canonical UI screenshot evidence currently focused on login/dashboard workflows.

The repository does not currently include a canonical semantic review window screenshot. Review behavior is primarily documented by tests in `DocumentManagement.Tests/Intelligence`, including snapshot, overlay traversal, focus restoration, stale refresh, and real-world coverage regressions.

To inspect the sample extraction from the console:

```powershell
dotnet run --project .\DocumentManagement.Intelligence.ConsoleTest\DocumentManagement.Intelligence.ConsoleTest.csproj -- .\artifacts\samples\decision_content_list_v2.json
```

The console output lists accepted candidates, rejected candidates, confidence reasoning, and field values. It is intended for debugging and review validation, not as a production analytics pipeline.

## Runtime And Operations

Runtime coordination is freshness-only. It guards refresh, cancellation, stale completion, overlay remap, and focus restoration state without owning semantics.

Operational hardening includes:

* startup validation for production JWT configuration.
* permission and security regression tests.
* bounded diagnostics rather than telemetry services.
* backup and restore services.
* runtime path validation.
* SQLite backup/restore coverage.
* publish scripts that refuse weak production secrets.

Operational observability is debug-oriented and observational only. It must not influence extraction, resolver ordering, review decisions, or overlay traversal.

## Known Limitations

Current limitations are explicit:

* OCR evidence is block/bbox-level; token/span bounding boxes are not authoritative.
* OCR may be corrupted, duplicated, malformed, or missing Vietnamese accents.
* Some page metrics may be inferred from block boxes.
* Real PDFs may have visual reading order that differs from JSON order.
* Signature and stamp areas may appear as ordinary paragraph blocks.
* Effective-date wording is preserved as body evidence; it is not a separate authoritative effective-date field.
* Extraction is heuristic and deterministic, not a generalized NLP reasoning engine.
* Low-confidence or missing values are expected when evidence is not explainable.
* Reviewers remain responsible for final business decisions.

## Non-Goals

This repository does not claim or implement:

* autonomous document approval.
* self-learning intelligence.
* LLM extraction or LLM ranking.
* fuzzy semantic inference.
* graph-based semantic reasoning.
* adaptive ranking or confidence feedback loops.
* ML orchestration.
* semantic cache hierarchies.
* replay/event sourcing systems.
* telemetry or analytics pipelines.
* token/span-level layout authority.

Predictable enterprise behavior is more important than unstable "smart" behavior.

## Running Locally

Requirements:

* .NET 8 SDK.
* Windows for the WPF client.
* PowerShell.
* Optional Tesseract installation for local OCR fallback.

Build the solution:

```powershell
dotnet build .\DocumentManagement.sln
```

Run the main test project:

```powershell
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj
```

If normal output folders are locked by a running process, use the alternate output path:

```powershell
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj -p:OutputPath="D:\QuanLyVanBan - V1.05\artifacts\test-out"
```

Run the WPF client:

```powershell
dotnet run --project .\DocumentManagement.Wpf\DocumentManagement.Wpf.csproj
```

Run the API from source:

```powershell
dotnet run --project .\DocumentManagement.Api\DocumentManagement.Api.csproj
```

Default health check:

```text
http://localhost:5033/health
```

## Configuration Notes

`DocumentManagement.Api/appsettings.json` contains development defaults only, including a clearly marked local JWT placeholder. Production deployments must provide a strong JWT secret through environment-specific configuration or a managed secret store.

Pilot deployments can use SQLite. SQL Server is supported for more durable multi-user deployments.

Do not commit production databases, runtime logs, generated publish folders, local API keys, real JWT secrets, or machine-specific production configuration. Use deployment templates and environment-specific configuration for real secrets.

See `docs/DEPLOYMENT.md` and `docs/security-hardening.md` for operational deployment and security guidance.

## Repository Hygiene

Canonical repository artifacts:

* source code.
* tests.
* documentation.
* deterministic sample fixtures under `artifacts/samples/`.
* canonical screenshots under `artifacts/screenshots/`.

Non-canonical generated content should stay out of Git:

* `bin/` and `obj/`.
* `publish/` and `artifacts/publish/`.
* `logs/` and `DocumentManagement.Api/logs/`.
* local databases and runtime storage.
* `artifacts/test-out/`.
* transient OCR proof files.
* copied binaries and zip packages.

Before publication or commit:

```powershell
git status
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj -p:OutputPath="D:\QuanLyVanBan - V1.05\artifacts\test-out"
dotnet build .\DocumentManagement.sln
```

After verification, remove generated `artifacts/test-out/` before publishing the repository unless it is needed temporarily for local diagnosis.
