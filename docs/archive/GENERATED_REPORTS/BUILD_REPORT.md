# Build And Startup Report

Generated: 2026-05-11

## Scope

Task: build the solution and identify startup blockers only.

No application code was modified.

## Restore

Command:

```powershell
dotnet restore .\DocumentManagement.sln
```

Result: Passed.

Output summary:

- All projects were up-to-date for restore.

## Build

Command:

```powershell
dotnet build .\DocumentManagement.sln --no-restore
```

Result: Passed.

Summary:

- Errors: 0
- Warnings: 0

Built projects:

- `DocumentManagement.Domain`
- `DocumentManagement.Contracts`
- `DocumentManagement.Application`
- `DocumentManagement.Infrastructure`
- `DocumentManagement.Wpf`
- `DocumentManagement.Api`

## Startup Validation

### Development

Command:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run --project .\DocumentManagement.Api\DocumentManagement.Api.csproj --no-build --urls http://127.0.0.1:5033
```

Result: Passed.

Observed:

```text
Now listening on: http://127.0.0.1:5033
Application started.
Hosting environment: Development
```

The command was stopped by timeout after confirming startup.

### Production Without Secret Override

Command:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Production'
dotnet run --project .\DocumentManagement.Api\DocumentManagement.Api.csproj --no-build --no-launch-profile --urls http://127.0.0.1:5034
```

Result: Failed fast.

Startup blocker:

```text
System.InvalidOperationException: Jwt:Secret production chưa được thay bằng giá trị bí mật an toàn.
```

Assessment:

- This is an expected deployment configuration blocker.
- The application intentionally refuses to start in Production with the checked-in placeholder JWT secret.
- This should not be fixed by weakening code or committing a real secret.

### Production With Temporary Environment Secret

Command:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Production'
$env:Jwt__Secret='LOCAL_STARTUP_VALIDATION_SECRET_32_CHARS_MIN_2026'
dotnet run --project .\DocumentManagement.Api\DocumentManagement.Api.csproj --no-build --no-launch-profile --urls http://127.0.0.1:5034
```

Result: Passed.

Observed:

```text
Now listening on: http://127.0.0.1:5034
Application started.
Hosting environment: Production
```

The command was stopped by timeout after confirming startup.

## Critical Startup Issues

| Severity | Issue | Type | Fix Applied |
| --- | --- | --- | --- |
| High | Production requires a non-placeholder `Jwt:Secret` | Deployment configuration | No code fix applied. Must be supplied via environment/secret store. |

## Conclusion

There are no code-level critical issues preventing startup.

The only startup blocker found is required production configuration: provide a real `Jwt__Secret` value outside source-controlled settings before running in Production.

