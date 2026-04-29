param(
    [switch]$SkipSmoke
)

$ErrorActionPreference = "Stop"

$Root = Resolve-Path "$PSScriptRoot\.."

function Pass($msg) { Write-Host "[PASS] $msg" -ForegroundColor Green }
function Fail($msg) { Write-Host "[FAIL] $msg" -ForegroundColor Red; exit 1 }
function Info($msg) { Write-Host "[INFO] $msg" -ForegroundColor Cyan }

Set-Location $Root

Write-Host "=== VERIFY START ==="

$projects = @(
    ".\DocumentManagement.Contracts\DocumentManagement.Contracts.csproj",
    ".\DocumentManagement.Domain\DocumentManagement.Domain.csproj",
    ".\DocumentManagement.Application\DocumentManagement.Application.csproj",
    ".\DocumentManagement.Infrastructure\DocumentManagement.Infrastructure.csproj",
    ".\DocumentManagement.Api\DocumentManagement.Api.csproj",
    ".\DocumentManagement.Wpf\DocumentManagement.Wpf.csproj",
    ".\DocumentManagement.Tests\DocumentManagement.Tests.csproj"
)

foreach ($project in $projects) {
    if (-not (Test-Path $project)) {
        Fail "Project not found: $project"
    }

    Info "Building $project"
    dotnet build $project
    if ($LASTEXITCODE -ne 0) {
        Fail "Build failed: $project"
    }
}

Pass "Build all projects OK"

Info "Running Architecture Check..."
powershell -ExecutionPolicy Bypass -File ".\tools\architecture-check.ps1"
if ($LASTEXITCODE -ne 0) {
    Fail "Architecture check failed"
}


dotnet test ".\DocumentManagement.Tests\DocumentManagement.Tests.csproj" --no-build
if ($LASTEXITCODE -ne 0) {
    Fail "Tests failed"
}

Pass "Tests OK"

if (-not $SkipSmoke) {
    powershell -ExecutionPolicy Bypass -File ".\tools\smoke-test.ps1"
    if ($LASTEXITCODE -ne 0) {
        Fail "Smoke test failed"
    }
}

Pass "VERIFY PASSED"
exit 0
