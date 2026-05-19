param(
    [string]$ApiBaseUrl = "http://localhost:5033/",
    [string]$ApiUrls = "http://0.0.0.0:5033",
    [ValidateSet("Sqlite", "SqlServer")]
    [string]$DatabaseProvider = "Sqlite",
    [string]$DatabasePath = "database/app.db",
    [string]$ConnectionString = "",
    [string]$JwtSecret = $env:DMS_JWT_SECRET,
    [string]$AdminSeedPassword = $env:DMS_ADMIN_SEED_PASSWORD,
    [string]$DataRoot = "",
    [string]$AttachmentsPath = "storage/attachments",
    [string]$BackupPath = "backups",
    [string]$LogFilePath = "logs/api-.log",
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"

$Root = Resolve-Path "$PSScriptRoot\.."
$ArtifactsRoot = Join-Path $Root "artifacts\publish"
$ApiProject = Join-Path $Root "DocumentManagement.Api\DocumentManagement.Api.csproj"
$WpfProject = Join-Path $Root "DocumentManagement.Wpf\DocumentManagement.Wpf.csproj"
$TestsProject = Join-Path $Root "DocumentManagement.Tests\DocumentManagement.Tests.csproj"
$ApiTemplate = Join-Path $Root "DocumentManagement.Api\appsettings.Production.template.json"
$ApiOut = Join-Path $ArtifactsRoot "api"
$WpfOut = Join-Path $ArtifactsRoot "wpf"
$ApiZip = Join-Path $ArtifactsRoot "DocumentManagement.Api-win-x64.zip"
$WpfZip = Join-Path $ArtifactsRoot "DocumentManagement.Wpf-win-x64.zip"
$ManifestPath = Join-Path $ArtifactsRoot "deployment-manifest.json"

function Info($message) { Write-Host "[INFO] $message" -ForegroundColor Cyan }
function Pass($message) { Write-Host "[PASS] $message" -ForegroundColor Green }
function Fail($message) { Write-Host "[FAIL] $message" -ForegroundColor Red; exit 1 }

function Assert-StrongSecret($secret) {
    if ([string]::IsNullOrWhiteSpace($secret)) { Fail "Production JWT secret is missing. Set -JwtSecret or DMS_JWT_SECRET." }
    if ($secret.Length -lt 48) { Fail "Production JWT secret must be at least 48 characters." }
    if ($secret -match "CHANGE_THIS|DEV_ONLY|PLACEHOLDER|DEFAULT|SECRET_KEY") { Fail "Production JWT secret is a placeholder." }
    if ($secret -notmatch "[a-z]" -or $secret -notmatch "[A-Z]" -or $secret -notmatch "[0-9]" -or $secret -notmatch "[^a-zA-Z0-9]") {
        Fail "Production JWT secret must include lowercase, uppercase, digit, and symbol characters."
    }
}

Assert-StrongSecret $JwtSecret

if ($DatabaseProvider -eq "SqlServer" -and [string]::IsNullOrWhiteSpace($ConnectionString)) {
    Fail "ConnectionString is required for SQL Server production publishing."
}

if (-not $ApiBaseUrl.EndsWith("/")) { $ApiBaseUrl = "$ApiBaseUrl/" }

Set-Location $Root

Info "Running required build..."
dotnet build ".\DocumentManagement.Wpf\DocumentManagement.Wpf.csproj" -c $Configuration
if ($LASTEXITCODE -ne 0) { Fail "WPF build failed." }

Info "Running tests..."
dotnet test $TestsProject -c $Configuration
if ($LASTEXITCODE -ne 0) { Fail "Tests failed." }

if (Test-Path $ArtifactsRoot) {
    Remove-Item -LiteralPath $ArtifactsRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $ApiOut | Out-Null
New-Item -ItemType Directory -Path $WpfOut | Out-Null

$selfContained = if ($FrameworkDependent) { "false" } else { "true" }

Info "Publishing API..."
dotnet publish $ApiProject -c $Configuration -r $RuntimeIdentifier --self-contained $selfContained -p:PublishSingleFile=false -p:PublishDir="$ApiOut\"
if ($LASTEXITCODE -ne 0) { Fail "API publish failed." }

Info "Publishing WPF client..."
dotnet publish $WpfProject -c $Configuration -r $RuntimeIdentifier --self-contained $selfContained -p:PublishSingleFile=false -p:PublishDir="$WpfOut\"
if ($LASTEXITCODE -ne 0) { Fail "WPF publish failed." }

$settings = Get-Content $ApiTemplate -Raw | ConvertFrom-Json
$settings.Database.Provider = $DatabaseProvider
$settings.Database.Path = $DatabasePath
$settings.Database.ConnectionString = $ConnectionString
$settings.Jwt.Secret = $JwtSecret
$settings.Kestrel.Endpoints.Http.Url = $ApiUrls

if (-not $settings.PSObject.Properties.Name.Contains("Storage")) {
    $settings | Add-Member -MemberType NoteProperty -Name Storage -Value ([pscustomobject]@{})
}
$settings.Storage | Add-Member -Force -MemberType NoteProperty -Name AttachmentsPath -Value $AttachmentsPath
$settings.Storage | Add-Member -Force -MemberType NoteProperty -Name DataRoot -Value $DataRoot

if (-not $settings.PSObject.Properties.Name.Contains("Backup")) {
    $settings | Add-Member -MemberType NoteProperty -Name Backup -Value ([pscustomobject]@{})
}
$settings.Backup | Add-Member -Force -MemberType NoteProperty -Name Path -Value $BackupPath

if (-not $settings.PSObject.Properties.Name.Contains("Logging")) {
    $settings | Add-Member -MemberType NoteProperty -Name Logging -Value ([pscustomobject]@{})
}
$settings.Logging | Add-Member -Force -MemberType NoteProperty -Name FilePath -Value $LogFilePath

if (-not [string]::IsNullOrWhiteSpace($AdminSeedPassword)) {
    if (-not $settings.PSObject.Properties.Name.Contains("AdminSeed")) {
        $settings | Add-Member -MemberType NoteProperty -Name AdminSeed -Value ([pscustomobject]@{})
    }
    $settings.AdminSeed | Add-Member -Force -MemberType NoteProperty -Name Password -Value $AdminSeedPassword
}

$settings | ConvertTo-Json -Depth 20 | Set-Content -Path (Join-Path $ApiOut "appsettings.Production.json") -Encoding UTF8

$wpfSettingsPath = Join-Path $WpfOut "appsettings.json"
if (Test-Path $wpfSettingsPath) {
    $wpfSettings = Get-Content $wpfSettingsPath -Raw | ConvertFrom-Json
    $wpfSettings.Api.BaseUrl = $ApiBaseUrl
    $wpfSettings | ConvertTo-Json -Depth 10 | Set-Content -Path $wpfSettingsPath -Encoding UTF8
}

Get-ChildItem $ApiOut -Recurse -Include *.db,*.sqlite,*.log | Remove-Item -Force -ErrorAction SilentlyContinue
Get-ChildItem $WpfOut -Recurse -Include *.db,*.sqlite,*.log | Remove-Item -Force -ErrorAction SilentlyContinue
foreach ($transientDir in @("database", "logs", "backups", "restore-temp", "storage")) {
    $candidate = Join-Path $ApiOut $transientDir
    if (Test-Path $candidate) {
        Remove-Item -LiteralPath $candidate -Recurse -Force
    }
}

$gitCommit = ""
try {
    $gitCommit = (git rev-parse --short HEAD 2>$null)
} catch {
    $gitCommit = ""
}

$manifest = [pscustomobject]@{
    product = "DocumentManagement"
    createdUtc = (Get-Date).ToUniversalTime().ToString("o")
    configuration = $Configuration
    runtimeIdentifier = $RuntimeIdentifier
    selfContained = (-not $FrameworkDependent.IsPresent)
    gitCommit = $gitCommit
    artifacts = [pscustomobject]@{
        apiFolder = "api"
        wpfFolder = "wpf"
        apiZip = "DocumentManagement.Api-win-x64.zip"
        wpfZip = "DocumentManagement.Wpf-win-x64.zip"
    }
    runtimeData = [pscustomobject]@{
        databaseProvider = $DatabaseProvider
        databasePath = $DatabasePath
        dataRoot = $DataRoot
        attachmentsPath = $AttachmentsPath
        backupPath = $BackupPath
        logFilePath = $LogFilePath
    }
    installerStatus = "MSI/MSIX not generated by this script; use these artifacts as signed installer inputs."
}

$manifest | ConvertTo-Json -Depth 10 | Set-Content -Path $ManifestPath -Encoding UTF8

Compress-Archive -Path (Join-Path $ApiOut "*") -DestinationPath $ApiZip -Force
Compress-Archive -Path (Join-Path $WpfOut "*") -DestinationPath $WpfZip -Force

Pass "Production publish artifacts created."
Info "API folder: $ApiOut"
Info "WPF folder: $WpfOut"
Info "API zip   : $ApiZip"
Info "WPF zip   : $WpfZip"
Info "Manifest  : $ManifestPath"
Info "Next steps: code-sign binaries, create signed MSIX/MSI with enterprise ACLs, and store secrets in machine/user environment or a managed secret store."
