# Session Checkpoint - 2026-05-14

This repository is in enterprise operational QA and finalization mode. It should evolve through small, evidence-driven fixes, not speculative redesign.

## Current Architecture State

Architecture remains:

* WPF desktop client.
* .NET 8.
* MVVM.
* Clean Architecture.
* API/Application/Domain/Infrastructure/Contracts separation.

Preserve:

* application-layer orchestration.
* service/repository boundaries.
* DataGrid virtualization and recycling.
* async cancellation and stale-completion guards.
* keyboard popup/IME routing protections.
* preview synchronization safety.
* lightweight custom chart rendering.

## Finalized Dashboard State

The dashboard now has a unified enterprise visual system across:

* `Cơ cấu hiệu lực văn bản`
* `Văn bản theo phòng ban`
* `Văn bản mới ban hành`
* `Văn bản ban hành theo tháng`

Current dashboard implementation:

* shared panel typography and title colors.
* shared panel border/frame language.
* custom Canvas radial chart with coordinated card/ring interaction.
* custom Canvas department 3D chart.
* custom Canvas monthly issued chart.
* realistic dashboard data from the API.
* screenshot evidence stored under `artifacts/screenshots/`.

Do not redesign the dashboard layout. Future dashboard work should address concrete QA issues inside the existing panel structure.

## Demo Data State

The API startup/demo-data path seeds 100 deterministic removable demo documents through real application/persistence services.

Demo markers:

* `document_number LIKE 'DEMO-2026-%'`
* `notes` contains `DEMO-QA-2026-05`

Verified expectations are covered by tests:

* 100 active marked demo documents after seed.
* idempotent reseed.
* safe cleanup removes the 100 marked demo rows.
* realistic department/type/month distribution.
* issued, active, expired, archived/completed, and draft/internal coverage.

Admin cleanup/reseed:

* `DELETE api/demo-data`
* `POST api/demo-data/seed`

## Authentication State

Login visual background has been softened while preserving the existing artwork and layout.

Authentication actions remain real/truthful:

* register flow creates a local/API staff account.
* forgot-password flow gives truthful local/admin recovery guidance.
* fake email reset, OAuth, and speculative identity systems remain out of scope.

## Screenshot Evidence

Canonical screenshot files:

* `artifacts/screenshots/dashboard-final-full.png`
* `artifacts/screenshots/dashboard-department-panel.png`
* `artifacts/screenshots/dashboard-radial-total-hover-full.png`
* `artifacts/screenshots/dashboard-radial-total-hover.png`
* `artifacts/screenshots/login-final.png`

Harness:

* `tools/DashboardScreenshotHarness/DashboardScreenshotHarness.csproj`

Run commands:

```powershell
dotnet run --project .\tools\DashboardScreenshotHarness\DashboardScreenshotHarness.csproj -- --api-url=http://localhost:5033/
dotnet run --project .\tools\DashboardScreenshotHarness\DashboardScreenshotHarness.csproj -- --api-url=http://localhost:5033/ radial-total-hover
dotnet run --project .\tools\DashboardScreenshotHarness\DashboardScreenshotHarness.csproj -- --api-url=http://localhost:5033/ login
```

## Verification Expectations

Always run:

```powershell
dotnet build .\DocumentManagement.Wpf\DocumentManagement.Wpf.csproj
```

Run tests when source behavior, data seeding, chart bindings, API/infrastructure behavior, or shared UI behavior changes:

```powershell
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj
```

If a running API process locks normal output, use:

```powershell
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj -p:OutputPath="D:\QuanLyVanBan - V1.05\artifacts\test-out\"
```

## Current Verified Results

Latest verification in this finalization pass:

* WPF build passed.
* Automated tests passed: 30/30.
* dashboard screenshot evidence refreshed.
* login screenshot evidence refreshed.
* radial `Tổng số` hover screenshot evidence refreshed.

## Forbidden Directions

Future sessions must not:

* redesign the application shell.
* replace real workflows with placeholders.
* introduce fake analytics or mock-only dashboard data.
* hide broken modules to avoid QA.
* add speculative AI/OCR/cloud identity workflows.
* introduce visual clutter, heavy animation, or startup-dashboard aesthetics.
* rework MVVM/runtime architecture without a concrete blocker.
