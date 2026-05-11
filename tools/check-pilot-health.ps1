param(
    [string]$ApiBase = "http://localhost:5034",
    [string]$LogDirectory = "C:\QuanLyVanBan\Api\logs",
    [string]$BackupDirectory = "C:\QuanLyVanBan\Backups",
    [string]$ServiceName = "DocumentManagement.Api.Pilot",
    [int]$BackupFreshHours = 24,
    [int]$MinimumFreeDiskPercent = 20
)

$ErrorActionPreference = "Stop"

function Pass($message) { Write-Host "[PASS] $message" -ForegroundColor Green }
function Warn($message) { Write-Host "[WARN] $message" -ForegroundColor Yellow }
function Fail($message) { Write-Host "[FAIL] $message" -ForegroundColor Red; $script:Failed = $true }

$script:Failed = $false

try {
    $response = Invoke-WebRequest "$ApiBase/health" -UseBasicParsing -TimeoutSec 10
    if ($response.StatusCode -eq 200) {
        Pass "/health returned 200"
    }
    else {
        Fail "/health returned $($response.StatusCode)"
    }
}
catch {
    Fail "/health failed: $($_.Exception.Message)"
}

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -eq "Running") {
        Pass "Service '$ServiceName' is running"
    }
    else {
        Fail "Service '$ServiceName' status is $($service.Status)"
    }
}
else {
    Warn "Service '$ServiceName' not found on this machine"
}

$systemDrive = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='$($env:SystemDrive)'"
if ($systemDrive -and $systemDrive.Size -gt 0) {
    $freePercent = [math]::Round(($systemDrive.FreeSpace / $systemDrive.Size) * 100, 1)
    if ($freePercent -ge $MinimumFreeDiskPercent) {
        Pass "Disk free space is $freePercent%"
    }
    else {
        Fail "Disk free space is $freePercent%, below $MinimumFreeDiskPercent%"
    }
}
else {
    Warn "Could not determine system drive free space"
}

if (Test-Path $BackupDirectory) {
    $latestBackup = Get-ChildItem -Path $BackupDirectory -File |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($latestBackup) {
        $ageHours = [math]::Round(((Get-Date) - $latestBackup.LastWriteTime).TotalHours, 1)
        if ($ageHours -le $BackupFreshHours) {
            Pass "Latest backup '$($latestBackup.Name)' is $ageHours hours old"
        }
        else {
            Fail "Latest backup '$($latestBackup.Name)' is $ageHours hours old"
        }
    }
    else {
        Fail "No backup files found in $BackupDirectory"
    }
}
else {
    Fail "Backup directory not found: $BackupDirectory"
}

if (Test-Path $LogDirectory) {
    $recentErrors = Get-ChildItem -Path $LogDirectory -Filter "*.log" -File |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 3 |
        Select-String -Pattern "\[ERR\]|\[FTL\]|Unhandled exception" -SimpleMatch:$false

    if ($recentErrors) {
        Warn "Recent error log entries found. Review API logs."
    }
    else {
        Pass "No recent fatal/error patterns found in latest logs"
    }
}
else {
    Warn "Log directory not found: $LogDirectory"
}

if ($script:Failed) {
    exit 1
}

Pass "Pilot operational checks completed"
exit 0
