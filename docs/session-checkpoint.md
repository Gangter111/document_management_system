# SESSION CHECKPOINT

This repository is a real enterprise WPF/.NET 8 Document Management System. It is no longer in speculative modernization mode.

Current work mode:

* enterprise operational QA
* gap remediation
* workflow trust
* runtime stability
* human-QA readiness

Do not treat this as a prototype, demo shell, startup dashboard, or UI concept.

## Current Architecture State

Architecture remains:

* WPF desktop client
* .NET 8
* MVVM
* Clean Architecture
* API/Application/Domain/Infrastructure/Contracts separation

Preserve:

* existing MVVM structure
* application-layer orchestration
* repository/service boundaries
* DataGrid virtualization and recycling
* lightweight row rendering
* async cancellation/lifecycle guards
* keyboard popup/IME routing protections
* preview synchronization safety

Do not rewrite architecture, command systems, paging, selection systems, or backend orchestration unless explicitly required by a concrete QA blocker.

## Completed Stabilization Work

Enterprise workflow stabilization is complete.

Completed runtime and workflow hardening:

* ViewModel deactivation/disposal safety
* stale async completion protection
* cancellation ownership hardening
* preview synchronization safety
* keyboard popup/IME routing safety
* export cancellation guards
* DataGrid virtualization preservation
* operational honesty cleanup

Completed DocumentList operational hardening:

* keyboard routing consistency
* button activation consistency
* stale selection cleanup
* paging consistency
* preview synchronization
* batch-selection trust fixes
* invalid filter state cleanup
* double-click row targeting protections
* command enable-state consistency
* permission-state cleanup

Completed visual refinement work:

* restrained Fluent cleanup
* rounded dashboard containers
* dashboard border hierarchy adjustments
* logo container refinement
* queue/sidebar spacing refinements
* preview empty-state refinement
* notification positioning remediation attempts

Approved visual direction:

* Fluent 2
* Windows 11
* Outlook/Teams-inspired enterprise software
* calm hierarchy
* clear but comfortable visual boundaries
* dense readability
* low eye fatigue
* operational scanability

Forbidden visual direction:

* startup-dashboard aesthetics
* gradients
* card-heavy redesigns
* decorative animations
* speculative polish passes
* ultra-flat muddy boundaries
* thick WinForms-style borders

## PDF Extraction State

PDF extraction has been improved for operational honesty.

Current state:

* PdfPig/native PDF text extraction remains primary.
* deterministic field extraction is preferred.
* OCR is not implemented.
* scanned-image PDFs must not silently fail.
* scanned-image PDFs should report: `Không tìm thấy text layer trong PDF. PDF scan ảnh hiện chưa hỗ trợ OCR.`

Forbidden:

* cloud OCR
* LLM-first extraction
* AI hallucination workflows
* fake OCR support
* claiming scanned PDFs are supported before OCR is actually implemented

Remaining PDF limitation:

* scanned PDFs still require a future local OCR fallback evaluation.

## Navigation / Module Status

Archive / Reports / Categories / System were restored and must remain visible.

Current intended state:

* Archive opens the real DocumentList archive queue and should show archived persisted documents.
* Reports opens a real statistics surface backed by dashboard/report API data.
* Categories opens real category/status lookup data.
* System opens current user/server/config information and admin operations.

Important:

* Placeholder-only modules are forbidden going forward.
* Do not hide broken modules to avoid QA findings.
* If a module is incomplete, make it truthfully QA-usable or fix the concrete defect.

Known Reports blocker history:

* A previous Reports implementation crashed with `Provide value on ...` and `A new guard page for the stack cannot be created.`
* Root cause found: Reports/Categories XAML referenced missing resources `FluentSubtitleTextStyle` and `FluentDataGridStyle`.
* Code fix applied: templates now use existing `SectionTitleTextStyle` and `AppDataGridStyle`.
* REQUIRED-NOW for next session: open Reports in the WPF UI and confirm no crash/modal-dialog spam remains.

## Authentication State

Dead visible login actions are forbidden.

Current state:

* Register action has been restored.
* Register uses a minimal local/API account creation flow with Staff role.
* Forgot password action has been restored.
* Forgot password truthfully states that email reset is not configured and gives local/admin recovery guidance.

Remaining QA:

* verify register dialog creates a usable account through the current API
* verify forgot-password guidance is visible, truthful, and non-blocking

Forbidden auth directions:

* fake email delivery
* cloud auth systems
* OAuth/social login
* speculative identity architecture
* hidden dead controls

## Demo Data State

The application now has a deterministic removable demo-data path.

Current implementation:

* API startup seeds exactly 40 demo documents through real application/persistence services.
* seeding is deterministic and idempotent.
* records do not duplicate on relaunch.
* demo rows are persisted in the real database and flow through the normal API/UI path.
* demo rows cover draft, issued/completed, urgent, active, expired, archived, pending processing, completed processing.
* there are at least 8 archived demo records.
* records use realistic Vietnamese titles, document numbers, departments, categories, senders, receivers, handlers, and dates.

Demo markers:

* document_number LIKE `DEMO-2026-%`
* notes contains `DEMO-QA-2026-05`

Cleanup path:

* API: `DELETE api/demo-data`
* System screen: `Clear Demo Data`

Reseed path:

* API: `POST api/demo-data/seed`
* System screen: `Seed Demo Data`

Cleanup rules:

* delete only marked demo records
* preserve real user data
* cleanup uses application/document service soft-delete path
* reseeding after cleanup must restore exactly 40 records

REQUIRED-NOW for next session:

* launch API/app and confirm exactly 40 demo documents exist
* confirm no duplicate records after restart
* confirm Clear Demo Data removes only demo rows
* confirm reseed restores exactly 40 rows

## QA Readiness Status

Ready for focused human QA:

* DocumentList search/filter/paging
* preview behavior
* archive queue visibility
* archive/restore using seeded archived records
* dashboard non-zero counts/charts from real data
* reports statistics from real data
* categories lookup visibility
* system server/user/config information
* demo seed/cleanup operations

Known unstable or needs-verification areas:

* Reports crash fix requires WPF UI verification.
* Archive / Reports / Categories / System should each be opened in the WPF UI to confirm stable navigation.
* notification/toast placement has code remediation but still needs visual validation across DPI/window sizes.
* scanned PDF OCR remains deferred.
* full VI/EN localization remains deferred.

## Critical Next Priorities

REQUIRED-NOW:

1. Verify Reports crash is fully fixed in the WPF UI:
   * no `Provide value on ...`
   * no `A new guard page for the stack cannot be created`
   * no repeated modal dialog spam

2. Verify exactly 40 removable demo documents:
   * mixed statuses
   * archive coverage
   * reporting coverage
   * paging/filter/search coverage
   * realistic Vietnamese data
   * deterministic and idempotent

3. Verify safe demo cleanup:
   * remove only demo data
   * preserve real user data
   * reseed works after cleanup

4. Verify modules are minimally QA-usable:
   * Reports
   * Archive
   * Categories
   * System

5. Verify login actions:
   * forgot password
   * register account

## Forbidden Directions

Future sessions must NOT:

* redesign the application
* rewrite MVVM architecture
* rewrite command architecture
* rewrite paging/selection systems
* introduce startup-dashboard aesthetics
* add speculative AI/OCR workflows
* introduce cloud dependencies
* hide broken features instead of fixing them
* replace real workflows with placeholder cards
* implement fake UI-only data
* create fake APIs
* create fake analytics
* create fake enterprise modules
* introduce animation-heavy behavior
* perform broad visual modernization
* re-polish stabilized screens without a concrete QA issue

## Verification Expectations

Always run:

```powershell
dotnet build .\DocumentManagement.Wpf\DocumentManagement.Wpf.csproj
```

When API/infrastructure/application changes are relevant, also run:

```powershell
dotnet build .\DocumentManagement.Api\DocumentManagement.Api.csproj
dotnet build .\DocumentManagement.Infrastructure\DocumentManagement.Infrastructure.csproj
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj
```

For demo-data work, verify through the real API path:

* login as admin
* `GET api/demo-data/count`
* `DELETE api/demo-data`
* `POST api/demo-data/seed`
* document search count
* archive status count
* dashboard count

Never claim success without build/test verification and, when requested, manual runtime verification.
