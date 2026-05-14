# Dashboard Agent Guide

This guide is the dashboard-specific companion to `AGENTS.md`. It documents the finalized component structure, chart rendering model, interaction expectations, screenshot workflow, and verification steps for future agents.

## Architecture Boundary

The dashboard remains a WPF/MVVM view backed by real API data.

* `DashboardView.xaml` owns panel composition and visual styles.
* `DashboardView.xaml.cs` owns only view-local interaction state that coordinates chart/card hover.
* `DashboardViewModel.cs` loads real dashboard statistics and computes display-only aggregates.
* Custom charts render with `Canvas.OnRender` and dependency properties.
* Business logic, persistence, and demo-data orchestration stay outside WPF.

Do not replace dashboard data with UI-only mock data.

## Panel Structure

The four standardized dashboard panels are:

* `Cơ cấu hiệu lực văn bản`
* `Văn bản theo phòng ban`
* `Văn bản mới ban hành`
* `Văn bản ban hành theo tháng`

The layout is intentionally stable. Future work should fix concrete QA issues inside an existing panel rather than rearranging the Tổng quan page.

## Custom Chart Rendering

The dashboard uses lightweight Canvas controls instead of template-heavy chart visuals for custom panels:

* `RadialDocumentChart`: semantic validity composition rings.
* `DepartmentDocuments3DChart`: restrained 3D department distribution.
* `MonthlyIssuedTimelineChart`: restrained monthly issued timeline.
* `DashboardChartDrawing`: shared frozen brush, typeface, gradient, and `FormattedText` helpers.

Rendering rules:

* freeze reusable brushes.
* prefer one `OnRender` path over nested visual trees.
* keep hover hit testing in the control.
* invalidate only when interaction/data changes.
* avoid converter-heavy rendering paths.
* keep animation subtle and lifecycle-safe; detach rendering handlers on unload.

## Radial Chart Interaction Logic

Semantic metric keys:

* `Total`
* `Issued`
* `Effective`
* `Expired`

Interaction contract:

* Ring hover sets `HoveredMetric`.
* Card hover sets the same `HoveredMetric`.
* Card click and ring click set `SelectedMetric`.
* Hover takes precedence over selected state while active.
* The gray `Total` ring must visibly highlight on hover/selection just like the colored rings.
* Non-active rings may dim, but selected/hovered rings must remain obvious.
* Center percentage text must remain centered and never collide with arcs.

## Styling Standards

Dashboard panels share one component language:

* title font: `Segoe UI`
* title size: `15`
* title weight: `Bold`
* title color: dark navy dashboard title brush
* label color: shared dark blue label brush
* muted labels: shared muted blue brush
* panel background: light near-white surface
* panel border: clear one-pixel premium outline
* radius: restrained, consistent panel radius
* shadows/glow: minimal and purposeful only

Do not introduce a second title style, panel frame, or decorative card family for a single panel.

## Demo Data Expectations

The dashboard should be validated against deterministic demo data seeded through real services:

* 100 demo documents.
* records marked with `DEMO-QA-2026-05`.
* document numbers matching `DEMO-2026-%`.
* multiple departments and document types.
* last-12-month distribution.
* meaningful issued, active, expired, archived/completed, and draft/internal coverage.

Charts should show real API-derived counts, not handcrafted WPF values.

## Screenshot Workflow

The screenshot harness lives in:

```text
tools/DashboardScreenshotHarness/
```

Before generating dashboard screenshots, confirm the API is running:

```powershell
Invoke-RestMethod http://localhost:5033/health
```

Generate final dashboard evidence:

```powershell
dotnet run --project .\tools\DashboardScreenshotHarness\DashboardScreenshotHarness.csproj -- --api-url=http://localhost:5033/
```

Generate radial interaction evidence:

```powershell
dotnet run --project .\tools\DashboardScreenshotHarness\DashboardScreenshotHarness.csproj -- --api-url=http://localhost:5033/ radial-total-hover
```

Generate login evidence:

```powershell
dotnet run --project .\tools\DashboardScreenshotHarness\DashboardScreenshotHarness.csproj -- --api-url=http://localhost:5033/ login
```

Canonical outputs:

* `artifacts/screenshots/dashboard-final-full.png`
* `artifacts/screenshots/dashboard-department-panel.png`
* `artifacts/screenshots/dashboard-radial-total-hover-full.png`
* `artifacts/screenshots/dashboard-radial-total-hover.png`
* `artifacts/screenshots/login-final.png`

Do not commit transient harness `bin/` or `obj/` directories.

## Verification Workflow

Minimum WPF verification:

```powershell
dotnet build .\DocumentManagement.Wpf\DocumentManagement.Wpf.csproj
```

Relevant automated tests:

```powershell
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj
```

If the normal test output is locked by a running API process:

```powershell
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj -p:OutputPath="D:\QuanLyVanBan - V1.05\artifacts\test-out\"
```

For demo-data changes, verify:

* `EnsureSeededAsync` remains idempotent.
* demo count remains 100.
* cleanup removes only marked demo records.
* dashboard charts populate from the API after reseed.

## Commit Hygiene

Before final commit:

* keep only canonical screenshots in `artifacts/screenshots/`.
* remove transient `artifacts/build-check`, `artifacts/test-out`, temporary databases, logs, and copied binaries.
* run build and tests.
* use terminal git workflow: `git status`, `git add .`, `git commit -m "..."`
