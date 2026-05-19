# AGENTS.md

## Project

This repository is a real enterprise Document Management System. It is not a prototype, demo shell, mock workflow, or UI-only application.

Tech stack:

* WPF desktop client
* .NET 8
* MVVM
* Clean Architecture
* ASP.NET Core API
* SQLite for local/pilot runtime, with SQL Server deployment support

Main projects:

* `DocumentManagement.Wpf`: desktop client and enterprise Fluent UI.
* `DocumentManagement.Api`: server API, authentication, persistence endpoints, backup/admin operations, and demo-data operations.
* `DocumentManagement.Application`: application orchestration and service contracts.
* `DocumentManagement.Domain`: entities, status semantics, and domain rules.
* `DocumentManagement.Infrastructure`: persistence, migrations, seed/demo data, OCR/PDF, file storage, backup, and export infrastructure.
* `DocumentManagement.Intelligence`: MinerU adaptation, deterministic semantic extraction, provenance, confidence/rejection reasoning, review evidence resolution, audit/replay snapshots, and overlay mapping.
* `DocumentManagement.Contracts`: DTO contracts shared by API and WPF.
* `DocumentManagement.Tests`: automated verification for migrations, services, security, intelligence behavior, review determinism, runtime safety, and UI-critical behavior.

## Architecture Boundaries

Preserve:

* MVVM structure and WPF binding boundaries.
* application-layer orchestration.
* API/Application/Domain/Infrastructure separation.
* backward compatibility with the existing OCR/autofill path.
* async cancellation and stale-completion guards.
* preview synchronization safety.
* keyboard popup/IME routing safety.
* DataGrid virtualization and recycling.
* lightweight row, chart, and overlay rendering.
* provenance-first review behavior and deterministic review snapshots.

Do not:

* rewrite backend architecture without an explicit QA blocker.
* move business logic into WPF.
* create fake APIs, fake analytics, fake modules, or UI-only data paths.
* hide broken modules to avoid defects.
* introduce CommandManager invalidation.
* introduce animation-heavy UI.
* introduce deep visual trees or converter-heavy styling.
* re-modernize stabilized screens without a concrete issue.
* replace the deterministic semantic review path with speculative AI behavior.

## Deterministic Semantic Review Platform

The current intelligence path is a deterministic semantic review platform:

```text
PDF
-> MinerU/OCR content list
-> DocumentStructure
-> SemanticExtractionPipeline
-> Evidence and provenance
-> Confidence and rejection reasoning
-> Review workflow
-> Resolver-normalized overlay review UI
```

Current capabilities:

* semantic extraction for issuer, document number, issue date, title/trich yeu, signer, recipients, and body sections.
* guarded real-world tuning for Vietnamese administrative and corporate documents.
* reject reasoning for unsafe candidates such as DOB-as-issue-date.
* multi-region review evidence with primary, supporting, and contextual roles.
* provenance metadata for extraction source, reject source, raw OCR text, normalized text, semantic role, and diagnostics.
* deterministic resolver ordering, semantic grouping, overlay traversal, focus restoration, replay/audit snapshots, runtime coordination, and operational observability.
* lightweight WPF overlay rendering and backward-compatible review workflow.

Current MinerU/OCR realities:

* sample output is flat block-based.
* evidence is bbox-level, not token-level.
* JSON order may differ from visual reading order.
* token/span bbox is not available.
* OCR may be noisy, corrupted, duplicated, malformed, or missing Vietnamese accents.
* page metrics may be inferred from block bboxes.
* signature/stamp areas may appear as paragraph blocks.

## Semantic Authority Boundaries

`ReviewEvidenceResolver` is the sole semantic ordering authority.

Mandatory invariants:

* overlay traversal consumes resolver-normalized order; it must not re-rank semantics.
* diagnostics, audit, replay, diff, observability, and profiling are observational only.
* runtime coordination owns freshness, cancellation, stale-completion rejection, and invalidation only; it must not own semantics.
* resolver output must not be mutated by overlay, diagnostics, audit, replay, or observability code.
* overlay consistency verification must compare against resolver-authoritative evidence identity, not overlay self-projections.
* stable semantic/evidence identity is preferred over coordinates.
* focus restoration must never become coordinate-only.
* primary evidence is the exact accepted-value source.
* supporting evidence explains semantic validity.
* contextual evidence gives ordered surroundings without becoming the value.

Repeated evidence rules:

* repeated header/footer suppression is scoped to a resolver invocation.
* repeated evidence is demoted with diagnostics, not removed.
* demotion must preserve provenance visibility and reviewer access.

## Determinism Invariants

Preserve deterministic behavior across:

* refresh.
* cancellation.
* evidence remap.
* repeated review open/close cycles.
* overlapping async review operations.
* virtualized overlay region reuse.
* rapid page switching.
* corrupted OCR and large evidence sets.
* replay, audit, diff, and operational snapshot generation.

Rules:

* latest refresh wins deterministically.
* stale overlay focus, traversal state, replay snapshots, and provenance diffs must be rejected or invalidated deterministically.
* stable ordering and stable traversal are required for reviewer trust.
* stable replay and focus restoration must be based on semantic/evidence identity before visual fallback.
* no timing-based behavior.
* no thread-scheduling assumptions in tests.
* no hash-order iteration in snapshots, ordering, traversal, replay, audit, diagnostics, or publication-visible output.
* no timestamps, random values, object references, hash-order dependence, or runtime-only identities in deterministic snapshots.
* no long-lived document-global mutable review history.
* no hidden background replay services.
* diagnostics must remain bounded, compact, deterministic, and non-authoritative.

## Real-World Tuning Philosophy

Real-world semantic coverage is improved by corpus-driven, lightweight additive heuristics only.

Allowed:

* Vietnamese administrative title heuristics.
* signer role heuristics.
* issuer-zone heuristics.
* recipient/footer heuristics.
* document-number normalization.
* OCR whitespace normalization.
* stamp/signature overlap tolerance.
* semantic grouping refinement that does not move authority out of the resolver.

Current tuned coverage includes:

* issuer candidates anchored by organization markers such as `CONG TY`, `CTY`, `UBND`, `HDND`, `SO`, `BO`, and guarded `VAN PHONG` header continuations.
* document numbers with noisy `So`, `S6`, `S0`, spaced `S o`, `No`, and `Ky hieu` markers, bounded by administrative stop words.
* title/trich yeu candidates for `QUYET DINH`, `THONG BAO`, `CONG VAN`, `TO TRINH`, `BAO CAO`, `KE HOACH`, `NGHI QUYET`, `V/v`, `Ve viec`, `Ve ...`, and `Trich yeu`.
* recipients from footer `Noi nhan` blocks and guarded left-aligned `Kinh gui` addressee lines.
* signer name promotion only when person-like text is found near signer role evidence.
* effective-date wording preserved in ordered body evidence rather than promoted as an unsupported separate authority.

Principles:

* inspect real OCR/corpus behavior before changing heuristics.
* prefer explainability over AI sophistication.
* prefer missing or low-confidence promotion over incorrect authoritative promotion.
* preserve raw/normalized OCR text and provenance for review.
* preserve existing OCR/autofill behavior and review UI.
* demote repeated evidence rather than removing it.
* keep tests deterministic with synthetic fixtures and committed sample corpora.

## Complexity Ceiling And Forbidden Expansions

Do not introduce:

* graph engines.
* adaptive ranking.
* ML orchestration.
* LLM extraction or LLM ranking.
* fuzzy semantic inference.
* confidence feedback loops.
* reactive frameworks.
* event buses.
* replay/event sourcing systems.
* semantic cache hierarchies.
* telemetry platforms or analytics pipelines.
* global diagnostic registries.
* speculative abstraction layers.
* generic semantic engines.
* token/span parsers that imply unavailable token-level bbox authority.

The system values predictable enterprise behavior over unstable "smart" behavior. Human-in-the-loop review is authoritative.

## Extraction Rules

Do not replace the old OCR flow, the existing WPF editor, or the review UI.

Semantic extraction rules:

* Issue date: hard reject blocks containing `sinh ngay`, `ngay sinh`, or `DOB`; DOB must never become issue date.
* Issuer: prioritize top-left administrative organization evidence; preserve nearby multiline header evidence; reject national headers and signer/recipient/body text as issuers.
* Document number: normalize OCR whitespace and noisy markers, but stop before date/title/subject wording.
* Title/trich yeu: promote administrative title markers and nearby subject continuations only when spatially plausible.
* Signer: signer name is primary evidence; role/title is supporting evidence; role keywords include `CHU TICH`, `GIAM DOC`, `TONG GIAM DOC`, `PHO GIAM DOC`, `KT.`, and `TM.`.
* Recipients: preserve ordered footer recipients and guarded direct `Kinh gui` addressees.
* Body: reconstruct Dieu sections in visual reading order and preserve ordered multi-block evidence safely.

Layout semantics:

* Top-left: issuer and document number.
* Top-center: title and decision type.
* Top-right: national header and issue date.
* Body: Dieu sections, paragraphs, clauses, and effective-date wording.
* Bottom-right: signer role, signer name, signature/stamp.
* Bottom-left: recipients and archive references.

## Review Runtime Invariants

Runtime coordination owns only freshness and cancellation safety.

Preserve:

* deterministic latest-refresh-wins behavior.
* stale completion rejection.
* cancellation propagation.
* overlay remap invalidation.
* focus restoration from stable semantic/evidence identity.
* bounded diagnostics.

Do not:

* make runtime coordination influence extraction or resolver ordering.
* introduce timing sleeps as correctness requirements.
* create document-global mutable review history.
* create hidden background replay services.

## Overlay And UI Rules

Overlay and review UI must remain lightweight.

Preserve:

* existing review workflow.
* overlay virtualization and rendering optimizations.
* deterministic overlay IDs, semantic group IDs, selection identity, and navigation behavior.
* custom drawing over deep visual trees.
* keyboard-first workflow.

Avoid:

* overlay rewrites.
* dispatcher-heavy diagnostic logic.
* visual-tree traversal for review state.
* allocation-heavy redraw paths.
* converter-heavy styling.
* UI cards or panels that change established review workflow without a concrete QA issue.

## Operational Observability

Operational observability is optional, debug-oriented, observational, and bounded.

Allowed:

* compact health diagnostics.
* deterministic failure reasons.
* resolver/overlay/traversal/focus consistency checks.
* compact runtime, replay, audit, diff, and profiling snapshots.

Not allowed:

* telemetry services.
* analytics pipelines.
* global diagnostic registries.
* background monitoring loops.
* verbose runtime logs as a substitute for deterministic tests.
* observability influencing resolver output or review workflow.

## Validation Philosophy

Real-world validation matters more than speculative architecture.

Use deterministic synthetic fixtures and committed sample corpora to verify:

* corporate decisions.
* official notices.
* large multi-page documents.
* repeated headers, footers, signers, and contextual evidence.
* corrupted OCR, missing accents, duplicate blocks, malformed whitespace, and partial ordering corruption.
* signer/stamp overlap.
* refresh/cancellation storms.
* overlay virtualization continuity.
* backward compatibility with existing review snapshots.

Avoid benchmark frameworks, telemetry infrastructure, adaptive heuristics, semantic caches, generalized NLP pipelines, and timing-based tests.

## Cleanup Philosophy

Cleanup is allowed only when it reduces risk without shifting authority.

Allowed:

* removing genuinely unused code.
* consolidating duplicate local helpers.
* clarifying names near existing ownership boundaries.
* tightening deterministic assertions.
* concise invariant comments near critical code.
* deleting stale experiments that are not part of the product path.
* removing generated logs, local databases, test outputs, and publish artifacts before publication.

Not allowed:

* architecture redesign.
* namespace revolutions.
* framework extraction.
* generic semantic engines.
* speculative abstractions.
* broad modernization rewrites.
* DB schema changes unless explicitly requested.
* moving review or semantic authority out of the resolver.
* deleting committed sample corpora or canonical screenshot evidence without an explicit reason.

Before cleanup, identify risks to semantic authority, deterministic behavior, snapshot stability, allocation behavior, and useful debugging infrastructure.

## Dashboard System

The dashboard must remain inside the existing Tong quan layout. Component-level refinements are allowed only inside the affected panel.

Current dashboard panels:

* Co cau hieu luc van ban: radial KPI chart and semantic metric cards.
* Van ban theo phong ban: lightweight Canvas 3D department chart.
* Van ban moi ban hanh: recent issued documents grid.
* Van ban ban hanh theo thang: lightweight Canvas monthly issued chart.

Dashboard styling standards:

* section titles use the shared `Segoe UI`, `15`, `Bold`, dark navy title treatment from `DashboardPremiumChartTitleStyle` / `PanelTitleStyle`.
* label text uses the shared dark blue label brush.
* subtle labels use the shared muted blue brush.
* panel frames use a light surface, clear one-pixel border, restrained radius, and no heavy decorative shadow.
* do not create isolated chart aesthetics.

## Demo Data

The deterministic demo-data path seeds 100 removable documents through real application/persistence services.

Demo records are identified by:

* `document_number LIKE 'DEMO-2026-%'`
* `notes` containing `DEMO-QA-2026-05`

Expected coverage:

* multiple departments.
* realistic Vietnamese document types and titles.
* last-12-month distribution.
* issued, active, expired, archived/completed, and draft/internal coverage.
* idempotent reseeding.
* safe cleanup that removes only marked demo records.

## Verification

Run before claiming success:

```powershell
dotnet build .\DocumentManagement.Wpf\DocumentManagement.Wpf.csproj
```

Run tests when changes touch source behavior, demo data, contracts, intelligence behavior, review behavior, runtime behavior, or shared UI behavior:

```powershell
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj
```

If normal build/test output is locked, use:

```powershell
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj -p:OutputPath="D:\QuanLyVanBan - V1.05\artifacts\test-out"
```

For publication-prep verification:

```powershell
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj -p:OutputPath="D:\QuanLyVanBan - V1.05\artifacts\test-out"
dotnet build .\DocumentManagement.sln
```

For full smoke verification when appropriate:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\verify.ps1 -SkipSmoke
powershell -ExecutionPolicy Bypass -File .\tools\verify-all.ps1
```

## Repository Hygiene

Commit source, docs, deterministic sample fixtures, and canonical screenshot evidence only. Keep temporary build outputs, transient databases, logs, copied binaries, and publish zips out of git.

Canonical artifacts:

* `artifacts/samples/decision_content_list_v2.json`
* `artifacts/screenshots/*.png`

Generated artifacts to remove before publication:

* `artifacts/test-out/`
* `artifacts/publish/`
* `artifacts/ocr-proof/`
* `publish/`
* `logs/`
* `DocumentManagement.Api/logs/`
* `database/`
* `DocumentManagement.Api/database/`
* local `.db`, `.sqlite`, `.sqlite3`, `.db-wal`, and `.db-shm` files.
* copied binaries and zip packages.

Before committing:

* run `git status`.
* remove temporary output folders under `artifacts/` unless they are canonical screenshots or committed fixtures.
* verify screenshots reflect the final UI state.
* run build and relevant tests.
* keep commits logically isolated.
* use a descriptive conventional commit message.

## Working Rules For Agents

* Read this file first and treat it as source of truth.
* Inspect existing code and tests before changing behavior.
* Prefer additive or reductive stabilization over rewrites.
* Preserve resolver authority, deterministic behavior, provenance, grouping, overlay traversal, focus restoration, runtime safety, and backward compatibility.
* Avoid unrelated refactors and UI churn.
* Build and run relevant tests after implementation.
* Summarize invariants preserved, cleanup risk, compatibility, and verification results after changes.
