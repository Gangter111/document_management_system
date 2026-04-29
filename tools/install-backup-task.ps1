param(
    [string]$TaskName = "QuanLyVanBan SQLite Backup",
    [string]$DatabasePath = "C:\QuanLyVanBan\Api\database\app.db",
    [string]$BackupDirectory = "C:\QuanLyVanBan\Backups",
    [string]$Time = "23:00",
    [int]$RetentionDays = 30
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
$backupScript = Join-Path $root "tools\backup-sqlite.ps1"

if (-not (Test-Path $backupScript)) {
    Fail "Backup script not found: $backupScript"
}

$actionArgs = "-NoProfile -ExecutionPolicy Bypass -File `"$backupScript`" -DatabasePath `"$DatabasePath`" -BackupDirectory `"$BackupDirectory`" -RetentionDays $RetentionDays"
$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument $actionArgs
$trigger = New-ScheduledTaskTrigger -Daily -At $Time
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -MultipleInstances IgnoreNew

Register-ScheduledTask `
    -TaskName $TaskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Description "Daily SQLite backup for QuanLyVanBan API." `
    -Force | Out-Null

Info "Task: $TaskName"
Info "Database: $DatabasePath"
Info "Backup directory: $BackupDirectory"
Info "Time: $Time"
Info "Retention days: $RetentionDays"
Pass "Backup scheduled task installed."
exit 0
