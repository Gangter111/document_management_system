param(
    [string]$TaskName = "QuanLyVanBan SQL Server Backup",
    [string]$ConnectionString = "Server=localhost\SQLEXPRESS;Database=DocumentManagementDb;Trusted_Connection=True;TrustServerCertificate=True",
    [string]$BackupDirectory = "C:\QuanLyVanBan\Backups",
    [string]$Time = "23:00",
    [int]$RetentionDays = 30,
    [switch]$Compress,
    [switch]$CopyOnly
)

$ErrorActionPreference = "Stop"

function Info($message) { Write-Host "[INFO] $message" -ForegroundColor Cyan }
function Pass($message) { Write-Host "[PASS] $message" -ForegroundColor Green }
function Fail($message) { Write-Host "[FAIL] $message" -ForegroundColor Red; exit 1 }

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).
    IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Fail "Run this script as Administrator."
}

$root = Resolve-Path "$PSScriptRoot\.."
$backupScript = Join-Path $root "tools\backup-sqlserver.ps1"

if (-not (Test-Path $backupScript)) {
    Fail "Backup script not found: $backupScript"
}

$compressArg = if ($Compress) { " -Compress" } else { "" }
$copyOnlyArg = if ($CopyOnly) { " -CopyOnly" } else { "" }
$actionArgs = "-NoProfile -ExecutionPolicy Bypass -File `"$backupScript`" -ConnectionString `"$ConnectionString`" -BackupDirectory `"$BackupDirectory`" -RetentionDays $RetentionDays$compressArg$copyOnlyArg"
$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument $actionArgs
$trigger = New-ScheduledTaskTrigger -Daily -At $Time
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -MultipleInstances IgnoreNew

Register-ScheduledTask `
    -TaskName $TaskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Description "Daily SQL Server backup for QuanLyVanBan API." `
    -Force | Out-Null

Info "Task: $TaskName"
Info "Backup directory: $BackupDirectory"
Info "Time: $Time"
Info "Retention days: $RetentionDays"
Pass "SQL Server backup scheduled task installed."
exit 0
