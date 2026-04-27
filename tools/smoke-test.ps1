param(
    [string]$BaseUrl = "http://localhost:5033"
)
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
chcp 65001 | Out-Null

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Pass {
    param([string]$Message)
    Write-Host "[PASS] $Message" -ForegroundColor Green
}

function Write-Fail {
    param([string]$Message)
    Write-Host "[FAIL] $Message" -ForegroundColor Red
}

function Invoke-ApiCheck {
    param(
        [string]$Name,
        [string]$Method,
        [string]$Url,
        [int[]]$ExpectedStatus,
        [string]$Role = "",
        [object]$Body = $null
    )

    $headers = @{}

    if (-not [string]::IsNullOrWhiteSpace($Role)) {
        $headers["X-User-Role"] = $Role
    }

    try {
        $params = @{
            Uri = $Url
            Method = $Method
            Headers = $headers
            UseBasicParsing = $true
        }

        if ($Body -ne $null) {
            $params["Body"] = ($Body | ConvertTo-Json -Depth 20)
            $params["ContentType"] = "application/json; charset=utf-8"
        }

        $response = Invoke-WebRequest @params
        $statusCode = [int]$response.StatusCode

        if ($ExpectedStatus -contains $statusCode) {
            Write-Pass "$Name => HTTP $statusCode"
            return @{
                Success = $true
                StatusCode = $statusCode
                Body = $response.Content
            }
        }

        Write-Fail "$Name => HTTP $statusCode, expected: $($ExpectedStatus -join ', ')"
        return @{
            Success = $false
            StatusCode = $statusCode
            Body = $response.Content
        }
    }
    catch {
        $statusCode = 0
        $bodyText = ""

        if ($_.Exception.Response -ne $null) {
            $statusCode = [int]$_.Exception.Response.StatusCode

            try {
                $stream = $_.Exception.Response.GetResponseStream()
                $reader = New-Object System.IO.StreamReader($stream)
                $bodyText = $reader.ReadToEnd()
            }
            catch {
                $bodyText = ""
            }
        }

        if ($ExpectedStatus -contains $statusCode) {
            Write-Pass "$Name => HTTP $statusCode"
            return @{
                Success = $true
                StatusCode = $statusCode
                Body = $bodyText
            }
        }

        Write-Fail "$Name => HTTP $statusCode, expected: $($ExpectedStatus -join ', ')"
        if (-not [string]::IsNullOrWhiteSpace($bodyText)) {
            Write-Host $bodyText -ForegroundColor DarkGray
        }

        return @{
            Success = $false
            StatusCode = $statusCode
            Body = $bodyText
        }
    }
}

function Convert-BodyToJson {
    param([string]$Body)

    if ([string]::IsNullOrWhiteSpace($Body)) {
        return $null
    }

    return $Body | ConvertFrom-Json
}

$BaseUrl = $BaseUrl.TrimEnd("/")

Write-Host "QLVB Smoke Test" -ForegroundColor Yellow
Write-Host "BaseUrl: $BaseUrl" -ForegroundColor Yellow

$failed = 0
$createdId = $null

Write-Step "Kiểm tra API sống"

$result = Invoke-ApiCheck `
    -Name "Swagger UI" `
    -Method "GET" `
    -Url "$BaseUrl/swagger/index.html" `
    -ExpectedStatus @(200)

if (-not $result.Success) { $failed++ }

Write-Step "Kiểm tra lookup API"

$result = Invoke-ApiCheck `
    -Name "GET categories" `
    -Method "GET" `
    -Url "$BaseUrl/api/lookups/categories" `
    -ExpectedStatus @(200)

if (-not $result.Success) { $failed++ }

$result = Invoke-ApiCheck `
    -Name "GET statuses" `
    -Method "GET" `
    -Url "$BaseUrl/api/lookups/statuses" `
    -ExpectedStatus @(200)

if (-not $result.Success) { $failed++ }

Write-Step "Kiểm tra dashboard API"

$result = Invoke-ApiCheck `
    -Name "GET dashboard" `
    -Method "GET" `
    -Url "$BaseUrl/api/dashboard" `
    -ExpectedStatus @(200)

if (-not $result.Success) {
    $failed++
}
else {
    $dashboard = Convert-BodyToJson $result.Body
    Write-Host "Dashboard totalDocuments: $($dashboard.summary.totalDocuments)" -ForegroundColor Gray
}

Write-Step "Kiểm tra documents API"

$result = Invoke-ApiCheck `
    -Name "GET documents" `
    -Method "GET" `
    -Url "$BaseUrl/api/documents" `
    -ExpectedStatus @(200)

if (-not $result.Success) { $failed++ }

$result = Invoke-ApiCheck `
    -Name "GET documents search" `
    -Method "GET" `
    -Url "$BaseUrl/api/documents/search?pageNumber=1&pageSize=10" `
    -ExpectedStatus @(200)

if (-not $result.Success) { $failed++ }

Write-Step "Kiểm tra quyền Staff được tạo mới"

$uniqueNumber = "SMOKE-" + (Get-Date -Format "yyyyMMdd-HHmmss")

$createBody = @{
    documentType = "INCOMING"
    documentNumber = $uniqueNumber
    title = "Smoke test document"
    issueDate = (Get-Date -Format "yyyy-MM-dd")
    receivedDate = (Get-Date -Format "yyyy-MM-dd")
    statusId = 4
    urgencyLevel = "NORMAL"
    confidentialityLevel = "NORMAL"
    senderName = "Smoke Test"
    processingDepartment = "Phòng Test"
    createdBy = "smoke-test"
}

$result = Invoke-ApiCheck `
    -Name "POST document as Staff" `
    -Method "POST" `
    -Url "$BaseUrl/api/documents" `
    -Role "Staff" `
    -Body $createBody `
    -ExpectedStatus @(200, 201)

if (-not $result.Success) {
    $failed++
}
else {
    $createdIdText = $result.Body.Trim()

    if ([long]::TryParse($createdIdText, [ref]$createdId)) {
        Write-Pass "Created document id = $createdId"
    }
    else {
        Write-Fail "Không đọc được id văn bản mới từ response: $createdIdText"
        $failed++
    }
}

if ($createdId -ne $null -and $createdId -gt 0) {
    Write-Step "Kiểm tra phân quyền sửa/xóa"

    $updateBody = @{
        id = $createdId
        documentType = "INCOMING"
        documentNumber = $uniqueNumber
        title = "Smoke test edited by staff"
        issueDate = (Get-Date -Format "yyyy-MM-dd")
        receivedDate = (Get-Date -Format "yyyy-MM-dd")
        statusId = 4
        urgencyLevel = "NORMAL"
        confidentialityLevel = "NORMAL"
        senderName = "Smoke Test"
        processingDepartment = "Phòng Test"
        updatedBy = "staff"
    }

    $result = Invoke-ApiCheck `
        -Name "PUT document as Staff should be forbidden" `
        -Method "PUT" `
        -Url "$BaseUrl/api/documents/$createdId" `
        -Role "Staff" `
        -Body $updateBody `
        -ExpectedStatus @(403)

    if (-not $result.Success) { $failed++ }

    $updateBody.title = "Smoke test edited by manager"
    $updateBody.updatedBy = "manager"

    $result = Invoke-ApiCheck `
        -Name "PUT document as Manager should pass" `
        -Method "PUT" `
        -Url "$BaseUrl/api/documents/$createdId" `
        -Role "Manager" `
        -Body $updateBody `
        -ExpectedStatus @(200, 204)

    if (-not $result.Success) { $failed++ }

    $result = Invoke-ApiCheck `
        -Name "DELETE document as Manager should be forbidden" `
        -Method "DELETE" `
        -Url "$BaseUrl/api/documents/$createdId" `
        -Role "Manager" `
        -ExpectedStatus @(403)

    if (-not $result.Success) { $failed++ }

    $result = Invoke-ApiCheck `
        -Name "DELETE document as Admin should pass" `
        -Method "DELETE" `
        -Url "$BaseUrl/api/documents/$createdId" `
        -Role "Admin" `
        -ExpectedStatus @(200, 204)

    if (-not $result.Success) { $failed++ }

    $result = Invoke-ApiCheck `
        -Name "GET deleted document should be not found" `
        -Method "GET" `
        -Url "$BaseUrl/api/documents/$createdId" `
        -ExpectedStatus @(404)

    if (-not $result.Success) { $failed++ }
}

Write-Step "Kết quả"

if ($failed -eq 0) {
    Write-Host "SMOKE TEST PASSED" -ForegroundColor Green
    exit 0
}

Write-Host "SMOKE TEST FAILED: $failed lỗi" -ForegroundColor Red
exit 1