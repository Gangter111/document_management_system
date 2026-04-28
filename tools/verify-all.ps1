$ErrorActionPreference = "Stop"

$Root = Resolve-Path "$PSScriptRoot\.."
$ApiProject = "$Root\DocumentManagement.Api\DocumentManagement.Api.csproj"
$Port = 5033

function Pass($msg) { Write-Host "[PASS] $msg" -ForegroundColor Green }
function Fail($msg) { Write-Host "[FAIL] $msg" -ForegroundColor Red; exit 1 }
function Info($msg) { Write-Host "[INFO] $msg" -ForegroundColor Cyan }

Set-Location $Root

Write-Host "=== VERIFY ALL START ==="

$connections = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue

if ($connections) {
    Info "Port $Port is already in use"

    foreach ($conn in $connections) {
        $pidValue = $conn.OwningProcess
        if ($pidValue -and $pidValue -ne 0) {
            try {
                $proc = Get-Process -Id $pidValue -ErrorAction SilentlyContinue
                if ($proc) {
                    Info "Killing process $($proc.ProcessName) PID=$pidValue on port $Port"
                    Stop-Process -Id $pidValue -Force
                    Start-Sleep -Seconds 2
                }
            }
            catch {}
        }
    }
}

if (-not (Test-Path $ApiProject)) {
    Fail "API project not found: $ApiProject"
}

Info "Starting API..."
$apiProcess = Start-Process powershell `
    -ArgumentList "-NoExit", "-ExecutionPolicy", "Bypass", "-Command", "cd '$Root'; dotnet run --project '$ApiProject' --urls http://localhost:$Port" `
    -PassThru

Start-Sleep -Seconds 8

try {
    powershell -ExecutionPolicy Bypass -File ".\tools\verify.ps1"
    if ($LASTEXITCODE -ne 0) {
        Fail "verify.ps1 failed"
    }
}
finally {
    Info "Stopping API process..."
    try {
        Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    }
    catch {}
}

Pass "VERIFY ALL PASSED"
exit 0