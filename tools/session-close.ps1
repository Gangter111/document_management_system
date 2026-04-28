$ErrorActionPreference = "Stop"

$Root = Resolve-Path "$PSScriptRoot\.."
$LogDir = "$Root\logs"
$Stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$LogFile = "$LogDir\session-close-$Stamp.log"

function Pass($msg) { Write-Host "[PASS] $msg" -ForegroundColor Green }
function Fail($msg) { Write-Host "[FAIL] $msg" -ForegroundColor Red; Stop-Transcript | Out-Null; exit 1 }
function Info($msg) { Write-Host "[INFO] $msg" -ForegroundColor Cyan }

Set-Location $Root

if (-not (Test-Path $LogDir)) {
    New-Item -ItemType Directory -Path $LogDir | Out-Null
}

Start-Transcript -Path $LogFile -Force | Out-Null

Write-Host "=== SESSION CLOSE START ==="
Info "Root: $Root"
Info "Log : $LogFile"

powershell -ExecutionPolicy Bypass -File ".\tools\verify-all.ps1"
if ($LASTEXITCODE -ne 0) {
    Fail "SESSION CLOSE FAILED"
}

Pass "SESSION CLOSE PASSED"

Stop-Transcript | Out-Null
exit 0