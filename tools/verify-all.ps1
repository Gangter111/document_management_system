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

Info "Running build/test verification before starting API..."
powershell -ExecutionPolicy Bypass -File ".\tools\verify.ps1" -SkipSmoke
if ($LASTEXITCODE -ne 0) {
    Fail "verify.ps1 failed"
}

if (-not (Test-Path $ApiProject)) {
    Fail "API project not found: $ApiProject"
}

Info "Starting API..."
$apiStdOut = Join-Path $Root "logs\api-verify-out.log"
$apiStdErr = Join-Path $Root "logs\api-verify-err.log"

Remove-Item -LiteralPath $apiStdOut, $apiStdErr -Force -ErrorAction SilentlyContinue

$apiProcess = Start-Process dotnet `
    -ArgumentList "run --no-build --project `"$ApiProject`" --urls http://localhost:$Port" `
    -RedirectStandardOutput $apiStdOut `
    -RedirectStandardError $apiStdErr `
    -PassThru

$ready = $false
for ($i = 1; $i -le 60; $i++) {
    try {
        Invoke-WebRequest "http://localhost:$Port/health" -UseBasicParsing -TimeoutSec 2 | Out-Null
        $ready = $true
        break
    }
    catch {
        Start-Sleep -Seconds 1
    }
}

if (-not $ready) {
    if (Test-Path $apiStdOut) {
        Get-Content $apiStdOut -Tail 80
    }

    if (Test-Path $apiStdErr) {
        Get-Content $apiStdErr -Tail 80
    }

    Fail "API did not become ready on port $Port"
}

try {
    powershell -ExecutionPolicy Bypass -File ".\tools\smoke-test.ps1"
    if ($LASTEXITCODE -ne 0) {
        Fail "Smoke test failed"
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
