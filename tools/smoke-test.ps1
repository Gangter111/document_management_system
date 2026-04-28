$ErrorActionPreference = "Stop"

$ApiBase = "http://localhost:5033"

function Pass($message) {
    Write-Host "[PASS] $message" -ForegroundColor Green
}

function Fail($message) {
    Write-Host "[FAIL] $message" -ForegroundColor Red
    exit 1
}

function Login($username, $password) {
    $body = @{
        username = $username
        password = $password
    } | ConvertTo-Json

    $response = Invoke-RestMethod `
        -Method POST `
        -Uri "$ApiBase/api/auth/login" `
        -ContentType "application/json" `
        -Body $body

    if ($response.token) { return $response.token }
    if ($response.accessToken) { return $response.accessToken }

    if ($response.data) {
        if ($response.data.token) { return $response.data.token }
        if ($response.data.accessToken) { return $response.data.accessToken }
    }

    Fail "JWT token not found"
}

function Get-DocumentId($response) {
    if ($null -eq $response) {
        return $null
    }

    if ($response -is [int]) {
        return $response
    }

    if ($response -is [long]) {
        return $response
    }

    if ($response -is [string] -and $response -match '^\d+$') {
        return [int]$response
    }

    if ($response.id) {
        return $response.id
    }

    if ($response.Id) {
        return $response.Id
    }

    if ($response.documentId) {
        return $response.documentId
    }

    if ($response.DocumentId) {
        return $response.DocumentId
    }

    if ($response.data) {
        if ($response.data -is [int]) {
            return $response.data
        }

        if ($response.data -is [long]) {
            return $response.data
        }

        if ($response.data -is [string] -and $response.data -match '^\d+$') {
            return [int]$response.data
        }

        if ($response.data.id) {
            return $response.data.id
        }

        if ($response.data.Id) {
            return $response.data.Id
        }

        if ($response.data.documentId) {
            return $response.data.documentId
        }

        if ($response.data.DocumentId) {
            return $response.data.DocumentId
        }
    }

    if ($response.result) {
        if ($response.result -is [int]) {
            return $response.result
        }

        if ($response.result -is [long]) {
            return $response.result
        }

        if ($response.result -is [string] -and $response.result -match '^\d+$') {
            return [int]$response.result
        }

        if ($response.result.id) {
            return $response.result.id
        }

        if ($response.result.Id) {
            return $response.result.Id
        }

        if ($response.result.documentId) {
            return $response.result.documentId
        }

        if ($response.result.DocumentId) {
            return $response.result.DocumentId
        }
    }

    return $null
}

function Invoke-ApiJson($method, $url, $token, $body = $null, $expectedStatusCode = $null) {
    $headers = @{}

    if ($token) {
        $headers["Authorization"] = "Bearer $token"
    }

    try {
        if ($null -ne $body) {
            $json = $body | ConvertTo-Json -Depth 20

            return Invoke-RestMethod `
                -Method $method `
                -Uri $url `
                -Headers $headers `
                -ContentType "application/json" `
                -Body $json
        }

        return Invoke-RestMethod `
            -Method $method `
            -Uri $url `
            -Headers $headers
    }
    catch {
        $statusCode = $null

        if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }

        if ($expectedStatusCode -and $statusCode -eq $expectedStatusCode) {
            return @{
                StatusCode = $statusCode
            }
        }

        throw
    }
}

Write-Host "=== SMOKE TEST START ==="

try {
    Invoke-WebRequest "$ApiBase/swagger/index.html" -UseBasicParsing | Out-Null
    Pass "Swagger OK"
}
catch {
    Fail "Swagger FAILED"
}

try {
    $adminToken = Login "admin" "admin123"
    Pass "Admin login JWT OK"
}
catch {
    Fail "Admin login FAILED"
}

try {
    $staffToken = Login "staff" "staff123"
    Pass "Staff login JWT OK"
}
catch {
    Fail "Staff login FAILED"
}

try {
    $managerToken = Login "manager" "manager123"
    Pass "Manager login JWT OK"
}
catch {
    Fail "Manager login FAILED"
}

try {
    Invoke-ApiJson "GET" "$ApiBase/api/dashboard" $adminToken | Out-Null
    Pass "Dashboard OK"
}
catch {
    Fail "Dashboard FAILED"
}

try {
    Invoke-ApiJson "GET" "$ApiBase/api/documents?page=1&pageSize=10" $adminToken | Out-Null
    Pass "Documents list OK"
}
catch {
    Fail "Documents list FAILED"
}

$stamp = Get-Date -Format "yyyyMMddHHmmss"

$createBody = @{
    documentNumber = "AUTO-$stamp"
    title = "Smoke Test Document $stamp"
    summary = "Created by smoke-test.ps1"
}

try {
    $created = Invoke-ApiJson "POST" "$ApiBase/api/documents" $staffToken $createBody
    $documentId = Get-DocumentId $created

    if (-not $documentId) {
        Write-Host "Create response:" -ForegroundColor Yellow
        $created | ConvertTo-Json -Depth 20
        Fail "Create document response has no id"
    }

    Pass "Staff create document OK: $documentId"
}
catch {
    Write-Host $_ -ForegroundColor Red
    Fail "Staff create document FAILED"
}

try {
    Invoke-ApiJson "PUT" "$ApiBase/api/documents/$documentId" $staffToken @{
        id = $documentId
        documentNumber = "AUTO-$stamp"
        title = "Smoke Test Staff Update Blocked $stamp"
        summary = "Staff update should be blocked"
    } 403 | Out-Null

    Pass "Staff update blocked 403 OK"
}
catch {
    Fail "Staff update permission FAILED"
}

try {
    Invoke-ApiJson "PUT" "$ApiBase/api/documents/$documentId" $managerToken @{
        id = $documentId
        documentNumber = "AUTO-$stamp"
        title = "Smoke Test Manager Updated $stamp"
        summary = "Updated by manager"
    } | Out-Null

    Pass "Manager update OK"
}
catch {
    Write-Host $_ -ForegroundColor Red
    Fail "Manager update FAILED"
}

try {
    Invoke-ApiJson "DELETE" "$ApiBase/api/documents/$documentId" $managerToken $null 403 | Out-Null
    Pass "Manager delete blocked 403 OK"
}
catch {
    Fail "Manager delete permission FAILED"
}

try {
    Invoke-ApiJson "DELETE" "$ApiBase/api/documents/$documentId" $adminToken | Out-Null
    Pass "Admin delete OK"
}
catch {
    Write-Host $_ -ForegroundColor Red
    Fail "Admin delete FAILED"
}

try {
    Invoke-ApiJson "GET" "$ApiBase/api/documents/$documentId" $adminToken $null 404 | Out-Null
    Pass "Deleted document returns 404 OK"
}
catch {
    Fail "Deleted document check FAILED"
}

Write-Host "SMOKE TEST PASSED" -ForegroundColor Green
exit 0