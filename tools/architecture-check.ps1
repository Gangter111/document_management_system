$ErrorActionPreference = "Stop"

$Root = Resolve-Path "$PSScriptRoot\.."

function Pass($msg) { Write-Host "[PASS] $msg" -ForegroundColor Green }
function Fail($msg) { Write-Host "[FAIL] $msg" -ForegroundColor Red; exit 1 }
function Info($msg) { Write-Host "[INFO] $msg" -ForegroundColor Cyan }

Set-Location $Root

Write-Host "=== ARCHITECTURE CHECK START ==="

# Định nghĩa strict rules cho toàn bộ dự án
$rules = @(
    @{
        Project = "DocumentManagement.Wpf\DocumentManagement.Wpf.csproj"
        Forbidden = @("DocumentManagement.Application", "DocumentManagement.Domain", "DocumentManagement.Infrastructure")
    },
    @{
        Project = "DocumentManagement.Domain\DocumentManagement.Domain.csproj"
        Forbidden = @("DocumentManagement.Application", "DocumentManagement.Infrastructure", "DocumentManagement.Api", "DocumentManagement.Wpf")
    },
    @{
        Project = "DocumentManagement.Application\DocumentManagement.Application.csproj"
        Forbidden = @("DocumentManagement.Infrastructure", "DocumentManagement.Api", "DocumentManagement.Wpf")
    },
    @{
        Project = "DocumentManagement.Infrastructure\DocumentManagement.Infrastructure.csproj"
        Forbidden = @("DocumentManagement.Api", "DocumentManagement.Wpf")
    }
)

$hasError = $false

foreach ($rule in $rules) {
    $projPath = Join-Path $Root $rule.Project
    if (-not (Test-Path $projPath)) {
        continue
    }

    $content = Get-Content $projPath -Raw
    foreach ($forbidden in $rule.Forbidden) {
        if ($content -match "Include=`".*$forbidden.*`"") {
            Write-Host "[FAIL] Architecture violation: $($rule.Project) references $forbidden" -ForegroundColor Red
            $hasError = $true
        }
    }
}

if ($hasError) {
    Fail "ARCHITECTURE CHECK FAILED"
}

Pass "ARCHITECTURE CHECK PASSED"
exit 0