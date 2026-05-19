param(
    [switch]$RunPublish,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$Root = Resolve-Path "$PSScriptRoot\.."
$WpfProject = Join-Path $Root "DocumentManagement.Wpf\DocumentManagement.Wpf.csproj"
$TestsProject = Join-Path $Root "DocumentManagement.Tests\DocumentManagement.Tests.csproj"
$ProductionTemplate = Join-Path $Root "DocumentManagement.Api\appsettings.Production.template.json"
$DeploymentDoc = Join-Path $Root "docs\DEPLOYMENT.md"
$SecurityDoc = Join-Path $Root "docs\security-hardening.md"
$PublishScript = Join-Path $Root "tools\publish-production.ps1"

function Info($message) { Write-Host "[INFO] $message" -ForegroundColor Cyan }
function Pass($message) { Write-Host "[PASS] $message" -ForegroundColor Green }
function Fail($message) { Write-Host "[FAIL] $message" -ForegroundColor Red; exit 1 }

Set-Location $Root

foreach ($required in @($ProductionTemplate, $DeploymentDoc, $SecurityDoc, $PublishScript)) {
    if (-not (Test-Path $required)) {
        Fail "Required release file is missing: $required"
    }
}

Pass "Required release files exist."

Info "Running WPF build..."
dotnet build $WpfProject -c $Configuration
if ($LASTEXITCODE -ne 0) { Fail "WPF build failed." }

Info "Running tests..."
dotnet test $TestsProject -c $Configuration
if ($LASTEXITCODE -ne 0) { Fail "Tests failed." }

if ($RunPublish) {
    if ([string]::IsNullOrWhiteSpace($env:DMS_JWT_SECRET)) {
        Fail "RunPublish requires DMS_JWT_SECRET."
    }

    Info "Running production publish..."
    powershell -ExecutionPolicy Bypass -File $PublishScript -Configuration $Configuration
    if ($LASTEXITCODE -ne 0) { Fail "Production publish failed." }
}
else {
    Info "Publish skipped. Use -RunPublish with DMS_JWT_SECRET set to verify publish artifacts."
}

Pass "Release verification completed."
Write-Host ""
Write-Host "Release readiness summary:" -ForegroundColor White
Write-Host "- Build: passed"
Write-Host "- Tests: passed"
Write-Host "- Production template: present"
Write-Host "- Deployment docs: present"
Write-Host "- Security hardening docs: present"
Write-Host "- Publish script: present"
if ($RunPublish) {
    Write-Host "- Publish: passed"
}
else {
    Write-Host "- Publish: skipped"
}
