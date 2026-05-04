param(
    [switch]$SkipSmoke
)

$ErrorActionPreference = "Stop"

$Root = Resolve-Path "$PSScriptRoot\.."

function Pass($msg) { Write-Host "[PASS] $msg" -ForegroundColor Green }
function Fail($msg) { Write-Host "[FAIL] $msg" -ForegroundColor Red; exit 1 }
function Info($msg) { Write-Host "[INFO] $msg" -ForegroundColor Cyan }

Set-Location $Root

if (-not $env:DOTNET_CLI_HOME) {
    $env:DOTNET_CLI_HOME = Join-Path $Root ".dotnet-home"
}

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"

New-Item -ItemType Directory -Path $env:DOTNET_CLI_HOME -Force | Out-Null
dotnet build-server shutdown | Out-Null

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
    dotnet build $project -m:1
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
