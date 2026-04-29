param(
    [string]$ApiBaseUrl = "http://localhost:5033/",
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"

$Root = Resolve-Path "$PSScriptRoot\.."
$Project = Join-Path $Root "DocumentManagement.Wpf\DocumentManagement.Wpf.csproj"
$PublishRoot = Join-Path $Root "publish\wpf-client"
$AppDir = Join-Path $PublishRoot "app"
$ZipPath = Join-Path $PublishRoot "DocumentManagement.Wpf-win-x64.zip"

function Info($message) { Write-Host "[INFO] $message" -ForegroundColor Cyan }
function Pass($message) { Write-Host "[PASS] $message" -ForegroundColor Green }
function Fail($message) { Write-Host "[FAIL] $message" -ForegroundColor Red; exit 1 }

if ([string]::IsNullOrWhiteSpace($ApiBaseUrl)) {
    Fail "ApiBaseUrl is required."
}

if (-not $ApiBaseUrl.EndsWith("/")) {
    $ApiBaseUrl = "$ApiBaseUrl/"
}

Set-Location $Root

Info "Root: $Root"
Info "Project: $Project"
Info "API: $ApiBaseUrl"
Info "Output: $AppDir"

if (Test-Path $PublishRoot) {
    Remove-Item -LiteralPath $PublishRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $AppDir | Out-Null

$selfContained = if ($FrameworkDependent) { "false" } else { "true" }

dotnet publish $Project `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained $selfContained `
    -p:PublishSingleFile=false `
    -p:PublishDir="$AppDir\"

if ($LASTEXITCODE -ne 0) {
    Fail "dotnet publish failed."
}

$appsettingsPath = Join-Path $AppDir "appsettings.json"

if (-not (Test-Path $appsettingsPath)) {
    Fail "appsettings.json not found in publish output."
}

$settings = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
$settings.Api.BaseUrl = $ApiBaseUrl
$settings | ConvertTo-Json -Depth 10 | Set-Content -Path $appsettingsPath -Encoding UTF8

Compress-Archive -Path (Join-Path $AppDir "*") -DestinationPath $ZipPath -Force

Pass "WPF client published."
Info "App folder: $AppDir"
Info "Zip file  : $ZipPath"
exit 0
