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

$VerifyAllScript = Join-Path $Root "tools\verify-all.ps1"
$ChecklistFile = Join-Path $Root "CHECKLIST_CHOT_PHIEN.md"

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

$start = Get-Date

try {
    Write-Host ""
    Write-Host "QLVB SESSION CLOSE CHECK" -ForegroundColor Cyan
    Write-Host "Root   : $Root"
    Write-Host "BaseUrl: $BaseUrl"

    Write-Section "CHECK REQUIRED FILES"

    Assert-FileExists -FilePath $VerifyAllScript -Label "verify-all.ps1"

    if (Test-Path $ChecklistFile) {
        Write-Pass "Co checklist chot phien"
    }
    else {
        Write-Warn "Chua co CHECKLIST_CHOT_PHIEN.md"
    }

    Write-Section "RUN VERIFY ALL"

    $argsList = @(
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $VerifyAllScript,
        "-BaseUrl",
        $BaseUrl
    )

    if ($KeepApiRunning) {
        $argsList += "-KeepApiRunning"
    }

    if ($SkipSmokeTest) {
        $argsList += "-SkipSmokeTest"
    }

    & powershell @argsList

    if ($LASTEXITCODE -ne 0) {
        throw "verify-all.ps1 failed."
    }

    $elapsed = New-TimeSpan -Start $start -End (Get-Date)

    Write-Host ""
    Write-Host "==================================================" -ForegroundColor DarkGray
    Write-Host "SESSION CLOSE PASSED" -ForegroundColor Green
    Write-Host ("Thoi gian: " + $elapsed.TotalSeconds.ToString("0.0") + " giay")
    Write-Host "==================================================" -ForegroundColor DarkGray

    Write-Host ""
    Write-Host "Co the chot phien." -ForegroundColor Green

    exit 0
}
catch {
    $elapsed = New-TimeSpan -Start $start -End (Get-Date)

    Write-Host ""
    Write-Host "==================================================" -ForegroundColor DarkGray
    Write-Host "SESSION CLOSE FAILED" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ("Thoi gian: " + $elapsed.TotalSeconds.ToString("0.0") + " giay")
    Write-Host "==================================================" -ForegroundColor DarkGray

    Write-Host ""
    Write-Host "Khong chot phien. Phai sua loi truoc." -ForegroundColor Red

    exit 1
}