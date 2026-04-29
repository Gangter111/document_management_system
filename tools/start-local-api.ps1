param(
    [string]$Urls = "http://localhost:5033"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path "$PSScriptRoot\.."
$apiDir = Join-Path $root "DocumentManagement.Api"
$dll = Join-Path $apiDir "bin\Debug\net8.0\DocumentManagement.Api.dll"
$project = Join-Path $apiDir "DocumentManagement.Api.csproj"

function Info($message) { Write-Host "[INFO] $message" -ForegroundColor Cyan }
function Pass($message) { Write-Host "[PASS] $message" -ForegroundColor Green }
function Fail($message) { Write-Host "[FAIL] $message" -ForegroundColor Red; exit 1 }

if (-not (Test-Path $dll)) {
    Info "API build output not found. Building API project..."
    $env:DOTNET_CLI_HOME = Join-Path $root ".dotnet-home"
    dotnet build $project

    if ($LASTEXITCODE -ne 0) {
        Fail "Build failed. Run dotnet test or build from Visual Studio, then try again."
    }
}

$connections = Get-NetTCPConnection -LocalPort 5033 -ErrorAction SilentlyContinue
if ($connections) {
    Pass "API port 5033 is already in use. The API may already be running."
    exit 0
}

$env:ASPNETCORE_ENVIRONMENT = "Development"

Set-Location $apiDir

Info "Starting DocumentManagement API"
Info "URL: $Urls"
Info "Content root: $apiDir"
Info "Press Ctrl+C to stop."

dotnet $dll --urls $Urls
