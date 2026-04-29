param(
    [string]$Urls = "http://0.0.0.0:5033",
    [ValidateSet("Sqlite", "SqlServer")]
    [string]$DatabaseProvider = "Sqlite",
    [string]$DatabasePath = "database/app.db",
    [string]$ConnectionString = "Server=localhost\SQLEXPRESS;Database=DocumentManagementDb;Trusted_Connection=True;TrustServerCertificate=True",
    [string]$JwtSecret = "CHANGE_THIS_TO_A_LONG_SECURE_SECRET_KEY_32_CHARS_MIN_2026",
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"

$Root = Resolve-Path "$PSScriptRoot\.."
$Project = Join-Path $Root "DocumentManagement.Api\DocumentManagement.Api.csproj"
$Template = Join-Path $Root "DocumentManagement.Api\appsettings.Production.template.json"
$PublishRoot = Join-Path $Root "publish\api-server"
$AppDir = Join-Path $PublishRoot "app"
$ZipPath = Join-Path $PublishRoot "DocumentManagement.Api-win-x64.zip"

function Info($message) { Write-Host "[INFO] $message" -ForegroundColor Cyan }
function Pass($message) { Write-Host "[PASS] $message" -ForegroundColor Green }
function Fail($message) { Write-Host "[FAIL] $message" -ForegroundColor Red; exit 1 }

if ([string]::IsNullOrWhiteSpace($Urls)) {
    Fail "Urls is required."
}

if ($DatabaseProvider -eq "Sqlite" -and [string]::IsNullOrWhiteSpace($DatabasePath)) {
    Fail "DatabasePath is required."
}

if ($DatabaseProvider -eq "SqlServer" -and [string]::IsNullOrWhiteSpace($ConnectionString)) {
    Fail "ConnectionString is required when DatabaseProvider is SqlServer."
}

if ([string]::IsNullOrWhiteSpace($JwtSecret) -or $JwtSecret.Length -lt 32) {
    Fail "JwtSecret must be at least 32 characters."
}

if (-not (Test-Path $Template)) {
    Fail "Production template not found: $Template"
}

Set-Location $Root

Info "Root: $Root"
Info "Project: $Project"
Info "Urls: $Urls"
Info "Database provider: $DatabaseProvider"
if ($DatabaseProvider -eq "SqlServer") {
    Info "SQL Server connection string: $ConnectionString"
}
else {
    Info "SQLite database: $DatabasePath"
}
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

$settings = Get-Content $Template -Raw | ConvertFrom-Json
$settings.Database.Provider = $DatabaseProvider
$settings.Database.Path = $DatabasePath
$settings.Database.ConnectionString = $ConnectionString
$settings.Jwt.Secret = $JwtSecret
$settings.Kestrel.Endpoints.Http.Url = $Urls

$appsettingsPath = Join-Path $AppDir "appsettings.Production.json"
$settings | ConvertTo-Json -Depth 20 | Set-Content -Path $appsettingsPath -Encoding UTF8

$runScriptPath = Join-Path $AppDir "run-api.ps1"
@'
$ErrorActionPreference = "Stop"
$env:ASPNETCORE_ENVIRONMENT = "Production"
Set-Location $PSScriptRoot
.\DocumentManagement.Api.exe
'@ | Set-Content -Path $runScriptPath -Encoding UTF8

$installServicePath = Join-Path $AppDir "install-service.ps1"
@'
param(
    [string]$ServiceName = "DocumentManagement.Api",
    [string]$DisplayName = "Document Management API",
    [string]$Description = "Internal API server for centralized document registry.",
    [string]$Environment = "Production"
)

$ErrorActionPreference = "Stop"

function Fail($message) { Write-Host "[FAIL] $message" -ForegroundColor Red; exit 1 }
function Pass($message) { Write-Host "[PASS] $message" -ForegroundColor Green }
function Info($message) { Write-Host "[INFO] $message" -ForegroundColor Cyan }

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).
    IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Fail "Run this script as Administrator."
}

$exePath = Join-Path $PSScriptRoot "DocumentManagement.Api.exe"

if (-not (Test-Path $exePath)) {
    Fail "Executable not found: $exePath"
}

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Fail "Service '$ServiceName' already exists. Run uninstall-service.ps1 first."
}

[Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", $Environment, "Machine")

New-Service `
    -Name $ServiceName `
    -BinaryPathName "`"$exePath`"" `
    -DisplayName $DisplayName `
    -Description $Description `
    -StartupType Automatic

Start-Service -Name $ServiceName

Info "Service: $ServiceName"
Info "Executable: $exePath"
Info "Environment: $Environment"
Pass "API Windows Service installed and started."
exit 0
'@ | Set-Content -Path $installServicePath -Encoding UTF8

$uninstallServicePath = Join-Path $AppDir "uninstall-service.ps1"
@'
param(
    [string]$ServiceName = "DocumentManagement.Api"
)

$ErrorActionPreference = "Stop"

function Fail($message) { Write-Host "[FAIL] $message" -ForegroundColor Red; exit 1 }
function Pass($message) { Write-Host "[PASS] $message" -ForegroundColor Green }
function Info($message) { Write-Host "[INFO] $message" -ForegroundColor Cyan }

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).
    IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Fail "Run this script as Administrator."
}

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $existing) {
    Pass "Service '$ServiceName' does not exist."
    exit 0
}

if ($existing.Status -ne "Stopped") {
    Stop-Service -Name $ServiceName -Force
}

sc.exe delete $ServiceName | Out-Null

Info "Service: $ServiceName"
Pass "API Windows Service removed."
exit 0
'@ | Set-Content -Path $uninstallServicePath -Encoding UTF8

Compress-Archive -Path (Join-Path $AppDir "*") -DestinationPath $ZipPath -Force

Pass "API server published."
Info "App folder: $AppDir"
Info "Zip file  : $ZipPath"
Info "Run script: $runScriptPath"
Info "Install   : $installServicePath"
Info "Uninstall : $uninstallServicePath"
exit 0
