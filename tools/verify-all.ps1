param(
    [string]$BaseUrl = "http://localhost:5033",
    [switch]$KeepApiRunning,
    [switch]$SkipSmokeTest
)

$ErrorActionPreference = "Stop"

try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding = [System.Text.Encoding]::UTF8
    chcp 65001 | Out-Null
}
catch {
}

if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    $Root = (Get-Location).Path
}
else {
    $Root = Split-Path -Parent $PSScriptRoot
}

$ApiProject = Join-Path $Root "DocumentManagement.Api\DocumentManagement.Api.csproj"
$VerifyScript = Join-Path $Root "tools\verify.ps1"
$LogDir = Join-Path $Root "logs"
$ApiStdOutLogPath = Join-Path $LogDir "verify-api-out.log"
$ApiStdErrLogPath = Join-Path $LogDir "verify-api-error.log"

function Write-Section {
    param([string]$Text)

    Write-Host ""
    Write-Host "==================================================" -ForegroundColor DarkGray
    Write-Host $Text -ForegroundColor Cyan
    Write-Host "==================================================" -ForegroundColor DarkGray
}

function Write-Pass {
    param([string]$Text)

    Write-Host "[PASS] $Text" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Text)

    Write-Host "[WARN] $Text" -ForegroundColor Yellow
}

function Assert-FileExists {
    param(
        [string]$FilePath,
        [string]$Label
    )

    if (-not (Test-Path $FilePath)) {
        throw "$Label khong ton tai: $FilePath"
    }
}

function Test-ApiAlive {
    param([string]$Url)

    try {
        $response = Invoke-WebRequest `
            -Uri "$Url/swagger/index.html" `
            -Method Get `
            -TimeoutSec 3 `
            -UseBasicParsing

        return $response.StatusCode -eq 200
    }
    catch {
        return $false
    }
}

function Wait-ApiReady {
    param(
        [string]$Url,
        [int]$TimeoutSeconds = 45
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        if (Test-ApiAlive -Url $Url) {
            return $true
        }

        Start-Sleep -Seconds 1
    }

    return $false
}

function Show-ApiLogs {
    if (Test-Path $ApiStdOutLogPath) {
        Write-Host ""
        Write-Host "API STDOUT LOG:" -ForegroundColor Yellow
        Get-Content $ApiStdOutLogPath -Tail 80 | Write-Host
    }

    if (Test-Path $ApiStdErrLogPath) {
        Write-Host ""
        Write-Host "API STDERR LOG:" -ForegroundColor Yellow
        Get-Content $ApiStdErrLogPath -Tail 80 | Write-Host
    }
}

$startedApiProcess = $null
$startedByScript = $false
$start = Get-Date

try {
    Write-Host ""
    Write-Host "QLVB VERIFY ALL" -ForegroundColor Cyan
    Write-Host "Root   : $Root"
    Write-Host "BaseUrl: $BaseUrl"

    Assert-FileExists -FilePath $ApiProject -Label "API project"
    Assert-FileExists -FilePath $VerifyScript -Label "verify.ps1"

    if (-not (Test-Path $LogDir)) {
        New-Item -ItemType Directory -Path $LogDir | Out-Null
    }

    Write-Section "CHECK API"

    if (Test-ApiAlive -Url $BaseUrl) {
        Write-Pass "API dang chay san tai $BaseUrl"
    }
    else {
        Write-Warn "API chua chay. Dang tu khoi dong API..."

        if (Test-Path $ApiStdOutLogPath) {
            Remove-Item $ApiStdOutLogPath -Force
        }

        if (Test-Path $ApiStdErrLogPath) {
            Remove-Item $ApiStdErrLogPath -Force
        }

        $startedApiProcess = Start-Process `
            -FilePath "dotnet" `
            -ArgumentList @(
                "run",
                "--project",
                $ApiProject,
                "--urls",
                $BaseUrl
            ) `
            -WorkingDirectory $Root `
            -RedirectStandardOutput $ApiStdOutLogPath `
            -RedirectStandardError $ApiStdErrLogPath `
            -PassThru `
            -WindowStyle Hidden

        $startedByScript = $true

        if (-not (Wait-ApiReady -Url $BaseUrl -TimeoutSeconds 45)) {
            Show-ApiLogs
            throw "Khoi dong API that bai hoac API khong san sang sau 45 giay."
        }

        Write-Pass "API da san sang"
    }

    Write-Section "RUN VERIFY"

    $verifyArgs = @(
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $VerifyScript,
        "-BaseUrl",
        $BaseUrl
    )

    if ($SkipSmokeTest) {
        $verifyArgs += "-SkipSmokeTest"
    }

    & powershell @verifyArgs

    if ($LASTEXITCODE -ne 0) {
        throw "verify.ps1 failed."
    }

    $elapsed = New-TimeSpan -Start $start -End (Get-Date)

    Write-Host ""
    Write-Host "==================================================" -ForegroundColor DarkGray
    Write-Host "VERIFY ALL PASSED" -ForegroundColor Green
    Write-Host ("Thoi gian: " + $elapsed.TotalSeconds.ToString("0.0") + " giay")
    Write-Host "==================================================" -ForegroundColor DarkGray

    exit 0
}
catch {
    $elapsed = New-TimeSpan -Start $start -End (Get-Date)

    Write-Host ""
    Write-Host "==================================================" -ForegroundColor DarkGray
    Write-Host "VERIFY ALL FAILED" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ("Thoi gian: " + $elapsed.TotalSeconds.ToString("0.0") + " giay")
    Write-Host "==================================================" -ForegroundColor DarkGray

    exit 1
}
finally {
    if ($startedByScript -and $startedApiProcess -ne $null -and -not $KeepApiRunning) {
        try {
            Write-Section "STOP API"

            Stop-Process -Id $startedApiProcess.Id -Force -ErrorAction SilentlyContinue

            Write-Pass "Da tat API do verify-all.ps1 khoi dong"
        }
        catch {
            Write-Warn "Khong tat duoc API process: $($_.Exception.Message)"
        }
    }
    elseif ($startedByScript -and $KeepApiRunning) {
        Write-Warn "Giu API dang chay theo tham so -KeepApiRunning"
        Write-Host "API PID: $($startedApiProcess.Id)"
        Write-Host "API stdout log: $ApiStdOutLogPath"
        Write-Host "API stderr log: $ApiStdErrLogPath"
    }
}