param(
    [string]$DatabasePath = "C:\QuanLyVanBan\Api\database\app.db",
    [string]$BackupDirectory = "C:\QuanLyVanBan\Backups",
    [int]$RetentionDays = 30
)

$ErrorActionPreference = "Stop"

function Info($message) { Write-Host "[INFO] $message" -ForegroundColor Cyan }
function Pass($message) { Write-Host "[PASS] $message" -ForegroundColor Green }
function Fail($message) { Write-Host "[FAIL] $message" -ForegroundColor Red; exit 1 }

if (-not (Test-Path $DatabasePath)) {
    Fail "Database file not found: $DatabasePath"
}

if ($RetentionDays -lt 1) {
    Fail "RetentionDays must be greater than 0."
}

New-Item -ItemType Directory -Path $BackupDirectory -Force | Out-Null

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupPath = Join-Path $BackupDirectory "app-$stamp.db"

Copy-Item -LiteralPath $DatabasePath -Destination $backupPath -Force

$cutoff = (Get-Date).AddDays(-$RetentionDays)

Get-ChildItem -Path $BackupDirectory -Filter "app-*.db" |
    Where-Object { $_.LastWriteTime -lt $cutoff } |
    Remove-Item -Force

Info "Database: $DatabasePath"
Info "Backup  : $backupPath"
Info "Retention days: $RetentionDays"
Pass "SQLite backup completed."
exit 0
