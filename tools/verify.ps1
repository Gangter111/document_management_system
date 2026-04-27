param(
    [string]$BaseUrl = "http://localhost:5033",
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

$ContractsProject = Join-Path $Root "DocumentManagement.Contracts\DocumentManagement.Contracts.csproj"
$ApiProject = Join-Path $Root "DocumentManagement.Api\DocumentManagement.Api.csproj"
$WpfProject = Join-Path $Root "DocumentManagement.Wpf\DocumentManagement.Wpf.csproj"
$WpfDir = Join-Path $Root "DocumentManagement.Wpf"
$SmokeTestScript = Join-Path $Root "tools\smoke-test.ps1"

$ForbiddenPatterns = @(
    "DocumentManagement.Application",
    "DocumentManagement.Domain",
    "DocumentManagement.Infrastructure",
    "Microsoft.Data.Sqlite",
    "SqliteConnection",
    "IDocumentRepository",
    "DatabaseInitializer"
)

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

function Write-Fail {
    param([string]$Text)

    Write-Host "[FAIL] $Text" -ForegroundColor Red
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

function Invoke-Build {
    param(
        [string]$ProjectPath,
        [string]$Name
    )

    Assert-FileExists -FilePath $ProjectPath -Label $Name

    Write-Section "BUILD $Name"

    dotnet build $ProjectPath --nologo

    if ($LASTEXITCODE -ne 0) {
        throw "Build that bai: $Name"
    }

    Write-Pass "Build $Name thanh cong"
}

function Invoke-ArchitectureCheck {
    Write-Section "ARCHITECTURE CHECK"

    if (-not (Test-Path $WpfDir)) {
        throw "Khong tim thay thu muc WPF: $WpfDir"
    }

    $files = Get-ChildItem $WpfDir -Recurse -File |
        Where-Object {
            ($_.Extension -eq ".cs" -or $_.Extension -eq ".xaml" -or $_.Extension -eq ".csproj") -and
            ($_.FullName -notlike "*\bin\*") -and
            ($_.FullName -notlike "*\obj\*")
        }

    $violations = New-Object System.Collections.Generic.List[object]

    foreach ($file in $files) {
        foreach ($pattern in $ForbiddenPatterns) {
            $matches = Select-String -Path $file.FullName -Pattern $pattern -SimpleMatch -ErrorAction SilentlyContinue

            foreach ($match in $matches) {
                $relativeFile = $file.FullName.Replace($Root + "\", "")

                $violations.Add([pscustomobject]@{
                    File = $relativeFile
                    Line = $match.LineNumber
                    Pattern = $pattern
                    Text = $match.Line.Trim()
                })
            }
        }
    }

    if ($violations.Count -gt 0) {
        Write-Fail "WPF dang vi pham kien truc client-server."

        $violations |
            Format-Table File, Line, Pattern, Text -AutoSize |
            Out-String -Width 240 |
            Write-Host

        throw "Architecture check that bai."
    }

    Write-Pass "WPF sach: khong tham chieu backend/internal database"
}

function Test-ApiAlive {
    param([string]$Url)

    try {
        $response = Invoke-WebRequest -Uri "$Url/swagger/index.html" -Method Get -TimeoutSec 5 -UseBasicParsing
        return $response.StatusCode -eq 200
    }
    catch {
        return $false
    }
}

function Invoke-SmokeTest {
    if ($SkipSmokeTest) {
        Write-Section "SMOKE TEST"
        Write-Host "[SKIP] Bo qua smoke test" -ForegroundColor Yellow
        return
    }

    Write-Section "SMOKE TEST"

    Assert-FileExists -FilePath $SmokeTestScript -Label "Smoke test script"

    if (-not (Test-ApiAlive -Url $BaseUrl)) {
        throw "API chua chay tai $BaseUrl. Hay chay API truoc."
    }

    powershell -ExecutionPolicy Bypass -File $SmokeTestScript -BaseUrl $BaseUrl

    if ($LASTEXITCODE -ne 0) {
        throw "Smoke test that bai."
    }

    Write-Pass "Smoke test thanh cong"
}

function Invoke-ProjectFileCheck {
    Write-Section "PROJECT FILE CHECK"

    Assert-FileExists -FilePath $ContractsProject -Label "Contracts project"
    Assert-FileExists -FilePath $ApiProject -Label "API project"
    Assert-FileExists -FilePath $WpfProject -Label "WPF project"

    Write-Pass "Du project file can kiem tra"
}

$start = Get-Date

try {
    Write-Host ""
    Write-Host "QLVB VERIFY" -ForegroundColor Cyan
    Write-Host "Root   : $Root"
    Write-Host "BaseUrl: $BaseUrl"

    Invoke-ProjectFileCheck

    Invoke-Build -ProjectPath $ContractsProject -Name "Contracts"
    Invoke-Build -ProjectPath $ApiProject -Name "API"
    Invoke-Build -ProjectPath $WpfProject -Name "WPF"

    Invoke-ArchitectureCheck

    Invoke-SmokeTest

    $elapsed = New-TimeSpan -Start $start -End (Get-Date)

    Write-Host ""
    Write-Host "==================================================" -ForegroundColor DarkGray
    Write-Host "VERIFY PASSED" -ForegroundColor Green
    Write-Host ("Thoi gian: " + $elapsed.TotalSeconds.ToString("0.0") + " giay")
    Write-Host "==================================================" -ForegroundColor DarkGray

    exit 0
}
catch {
    $elapsed = New-TimeSpan -Start $start -End (Get-Date)

    Write-Host ""
    Write-Host "==================================================" -ForegroundColor DarkGray
    Write-Host "VERIFY FAILED" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ("Thoi gian: " + $elapsed.TotalSeconds.ToString("0.0") + " giay")
    Write-Host "==================================================" -ForegroundColor DarkGray

    exit 1
}