# AGENTS.md

# Project Overview

This repository is a real enterprise Document Management System, not a prototype, demo shell, UI concept, or mock workflow application.

Tech stack:

* WPF desktop client
* .NET 8
* MVVM
* Clean Architecture
* ASP.NET Core API
* SQLite for local/pilot runtime, with SQL Server deployment support

Main projects:

* `DocumentManagement.Wpf`: desktop client and enterprise Fluent UI.
* `DocumentManagement.Api`: server API, authentication, persistence endpoints, demo-data operations.
* `DocumentManagement.Application`: application orchestration and service contracts.
* `DocumentManagement.Domain`: entities, status semantics, and domain rules.
* `DocumentManagement.Infrastructure`: persistence, migrations, seed/demo data, file/PDF/export infrastructure.
* `DocumentManagement.Contracts`: DTO contracts shared by API and WPF.
* `DocumentManagement.Tests`: automated verification for migrations, seed data, services, and safety-critical behavior.

# Engineering Rules

Preserve:

* MVVM structure and WPF binding boundaries.
* Application-layer orchestration.
* API/Application/Domain/Infrastructure separation.
* async cancellation and stale-completion guards.
* preview synchronization safety.
* keyboard popup/IME routing safety.
* DataGrid virtualization and recycling.
* lightweight row and chart rendering.

Do not:

* rewrite backend architecture without an explicit QA blocker.
* move business logic into WPF.
* create fake APIs, fake analytics, fake modules, or UI-only data paths.
* hide broken modules to avoid defects.
* introduce CommandManager invalidation.
* introduce animation-heavy UI.
* introduce deep visual trees or converter-heavy styling.
* re-modernize stabilized screens without a concrete issue.

# UI Direction

The UI target is restrained enterprise software:

* Fluent 2 / Windows 11 inspired.
* Outlook/Teams operational density.
* calm hierarchy.
* clear panel boundaries.
* low eye fatigue.
* keyboard-first workflow.
* predictable runtime behavior.

Avoid:

* startup-dashboard aesthetics.
* infographic-heavy pages.
* decorative gradients and visual clutter.
* heavy shadows, heavy glow, and excessive animation.
* speculative polish that does not fix a concrete QA issue.

# Dashboard System

The dashboard must remain inside the existing Tổng quan layout. Component-level refinements are allowed only inside the affected panel.

Current dashboard panels:

* `Cơ cấu hiệu lực văn bản`: radial KPI chart and semantic metric cards.
* `Văn bản theo phòng ban`: lightweight Canvas 3D department chart.
* `Văn bản mới ban hành`: recent issued documents grid.
* `Văn bản ban hành theo tháng`: lightweight Canvas monthly issued chart.

Dashboard files:

* `DocumentManagement.Wpf/Views/DashboardView.xaml`: panel layout, shared dashboard styles, typography, panel frame standards, chart bindings.
* `DocumentManagement.Wpf/Views/DashboardView.xaml.cs`: view-owned hover state for radial chart/card coordination.
* `DocumentManagement.Wpf/ViewModels/DashboardViewModel.cs`: dashboard data shaping and derived display metrics.
* `DocumentManagement.Wpf/Controls/RadialDocumentChart.cs`: custom Canvas radial validity chart.
* `DocumentManagement.Wpf/Controls/DashboardKpiCard.cs`: semantic metric cards bound to the radial chart.
* `DocumentManagement.Wpf/Controls/DepartmentDocuments3DChart.cs`: custom Canvas department chart.
* `DocumentManagement.Wpf/Controls/MonthlyIssuedTimelineChart.cs`: custom Canvas monthly issued chart.
* `DocumentManagement.Wpf/Controls/DashboardChartDrawing.cs`: shared frozen brush/typeface/drawing helpers for dashboard charts.

Dashboard styling standards:

* All four dashboard section titles use the shared `Segoe UI`, `15`, `Bold`, dark navy title treatment from `DashboardPremiumChartTitleStyle` / `PanelTitleStyle`.
* Dashboard label text uses the shared dark blue label brush.
* Subtle labels use the shared muted blue brush.
* Panel frames use the same sharp, premium border family: light surface, clear one-pixel border, restrained radius, no heavy decorative shadow.
* Do not create isolated chart aesthetics. Chart panels should feel like one component system while preserving each chart's identity.

Radial chart interaction expectations:

* Semantic groups are `Total`, `Issued`, `Effective`, and `Expired`.
* Hovering or selecting a legend card updates the matching chart ring.
* Hovering the chart ring updates the corresponding card/ring state.
* The gray `Tổng số` ring must provide clear feedback on hover and selection, consistent with the colored rings.
* Center percentage typography must remain readable but must not collide visually with the arcs.

# Demo Data

The deterministic demo-data path currently seeds 100 removable documents through real application/persistence services.

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

Admin API operations:

* `GET api/demo-data/count`
* `POST api/demo-data/seed`
* `DELETE api/demo-data`

# Screenshot Evidence

Canonical screenshot evidence belongs in `artifacts/screenshots/`.

Use the committed screenshot harness:

```powershell
dotnet run --project .\tools\DashboardScreenshotHarness\DashboardScreenshotHarness.csproj -- --api-url=http://localhost:5033/
dotnet run --project .\tools\DashboardScreenshotHarness\DashboardScreenshotHarness.csproj -- --api-url=http://localhost:5033/ radial-total-hover
dotnet run --project .\tools\DashboardScreenshotHarness\DashboardScreenshotHarness.csproj -- --api-url=http://localhost:5033/ login
```

Canonical outputs:

* `artifacts/screenshots/dashboard-final-full.png`
* `artifacts/screenshots/dashboard-department-panel.png`
* `artifacts/screenshots/dashboard-radial-total-hover-full.png`
* `artifacts/screenshots/dashboard-radial-total-hover.png`
* `artifacts/screenshots/login-final.png`

# Verification Workflow

Always run before claiming success:

```powershell
dotnet build .\DocumentManagement.Wpf\DocumentManagement.Wpf.csproj
```

Run tests when the change touches source behavior, demo data, API/application/infrastructure contracts, or shared UI behavior:

```powershell
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj
```

If a running API locks normal build/test output, use an alternate test output path:

```powershell
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj -p:OutputPath="D:\QuanLyVanBan - V1.05\artifacts\test-out\"
```

For full smoke verification when appropriate:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\verify.ps1 -SkipSmoke
powershell -ExecutionPolicy Bypass -File .\tools\verify-all.ps1
```

When generating dashboard screenshots, ensure the API is running and healthy:

```powershell
Invoke-RestMethod http://localhost:5033/health
```

# Repository Hygiene

Commit source, docs, and canonical screenshot evidence only. Keep temporary build outputs, transient databases, logs, and copied binaries out of git.

Before committing:

* run `git status`.
* remove temporary output folders under `artifacts/` unless they are canonical screenshots.
* verify screenshots reflect the final UI state.
* run build and relevant tests.
* use a descriptive conventional commit message.
