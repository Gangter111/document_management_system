# Staging Deployment Report

Generated: 2026-05-11

## Scope

Prepared and validated a staging deployment path for the ASP.NET Core API.

This pass did not install a Windows Service or deploy to a remote server. It created and validated a local staging artifact.

## Changes Applied

### Staging Configuration Template

File:

- `DocumentManagement.Api/appsettings.Staging.template.json`

Purpose:

- Provides an explicit staging environment template.
- Defaults staging to:
  - SQLite database path: `database/staging-app.db`
  - API URL: `http://0.0.0.0:5034`
  - API restore disabled
  - login rate limit: 60 attempts / 60 seconds

### Staging-Aware Publish Script

File:

- `tools/publish-api.ps1`

Changes:

- Added `-Environment Production|Staging`.
- Uses `appsettings.$Environment.template.json`.
- Generates `appsettings.$Environment.json`.
- Generates `run-api.ps1` with the selected environment.
- Staging artifacts are written to `publish\api-server-staging`.
- Generated Windows Service installer passes `--environment "<Environment>"` to the service binary instead of setting `ASPNETCORE_ENVIRONMENT` at machine scope.

Risk reduced:

- Staging and Production can coexist more safely without machine-wide environment variable collisions.

Regression risk:

- Operators relying on machine-level `ASPNETCORE_ENVIRONMENT` from the generated installer should use the generated service command line or pass `-Environment` explicitly.

## Artifact Created

Command:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\publish-api.ps1 `
  -Environment Staging `
  -Urls http://127.0.0.1:5047 `
  -DatabaseProvider Sqlite `
  -DatabasePath database/staging-app.db `
  -JwtSecret STAGING_SECRET_FOR_VALIDATION_1234567890 `
  -FrameworkDependent
```

Output:

- App folder: `publish\api-server-staging\app`
- ZIP artifact: `publish\api-server-staging\DocumentManagement.Api-Staging-win-x64.zip`
- Generated config: `publish\api-server-staging\app\appsettings.Staging.json`
- Run script: `publish\api-server-staging\app\run-api.ps1`
- Service installer: `publish\api-server-staging\app\install-service.ps1`

## Validation Results

Build:

```powershell
dotnet build .\DocumentManagement.sln --no-restore
```

Result:

- Passed
- Warnings: 0
- Errors: 0

Tests:

```powershell
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj
```

Result:

- Passed: 44
- Failed: 0
- Skipped: 0
- Total: 44

Staging artifact health check:

- Started `publish\api-server-staging\app\DocumentManagement.Api.exe`
- Environment: `Staging`
- URL: `http://127.0.0.1:5047`
- `/health` returned `200`

Staging smoke test:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\smoke-test.ps1 `
  -ApiBase http://127.0.0.1:5047
```

Result:

- Health OK
- Admin login OK
- Staff login OK
- Manager login OK
- Publisher login OK
- Dashboard OK
- Documents list OK
- Staff create OK
- Staff update blocked
- Publisher update OK
- Publisher delete blocked
- Manager update OK
- Manager delete blocked
- Admin delete OK
- Deleted document returns 404
- Smoke test passed

## Warning Observed

During `dotnet publish`, NuGet vulnerability feed checks emitted `NU1900` warnings because this sandbox cannot reach `https://api.nuget.org/v3/index.json`.

Impact:

- Publish completed successfully.
- This does not prove package vulnerability status.

Required before promoting staging:

- Re-run publish or vulnerability audit in an environment with NuGet feed access.

## Staging Deployment Checklist

- [ ] Use a staging-specific JWT secret, not the validation secret from this report.
- [ ] Use a staging database isolated from development and production.
- [ ] Use a staging port or hostname that cannot collide with production.
- [ ] Keep `Backup:AllowApiRestore=false` unless testing restore in a maintenance window.
- [ ] Install service with staging-specific service name if running side-by-side:

```powershell
powershell -ExecutionPolicy Bypass -File .\install-service.ps1 `
  -ServiceName DocumentManagement.Api.Staging `
  -DisplayName "Document Management API - Staging" `
  -Environment Staging
```

- [ ] Restrict staging API to approved internal testers.
- [ ] Rotate seeded/default passwords before user acceptance testing.
- [ ] Run smoke test after deployment.
- [ ] Capture staging logs and backup artifacts separately from production.

## Go / No-Go

Staging package status: **GO for controlled staging validation**.

Do not promote to production from this staging run until:

- NuGet vulnerability checks are completed with network access.
- Staging JWT secret is replaced.
- Default user passwords are rotated.
- Staging backup and restore path is tested.
