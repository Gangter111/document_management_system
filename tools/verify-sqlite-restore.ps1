param(
    [Parameter(Mandatory = $true)]
    [string]$BackupPath,
    [string]$WorkingDirectory = "$env:TEMP\QuanLyVanBanRestoreVerify",
    [string]$SqliteAssemblyPath = ".\publish\api-server-staging\app\Microsoft.Data.Sqlite.dll"
)

$ErrorActionPreference = "Stop"

function Pass($message) { Write-Host "[PASS] $message" -ForegroundColor Green }
function Fail($message) { Write-Host "[FAIL] $message" -ForegroundColor Red; exit 1 }

if (-not (Test-Path $BackupPath)) {
    Fail "Backup file not found: $BackupPath"
}

$extension = [System.IO.Path]::GetExtension($BackupPath)
if ($extension -notin @(".db", ".sqlite")) {
    Fail "Only .db and .sqlite backups are supported by this verifier."
}

New-Item -ItemType Directory -Path $WorkingDirectory -Force | Out-Null

$copyPath = Join-Path $WorkingDirectory ("restore-verify-" + [guid]::NewGuid().ToString("N") + $extension)
Copy-Item -LiteralPath $BackupPath -Destination $copyPath -Force

try {
    $bytes = [System.IO.File]::ReadAllBytes($copyPath)
    $header = [System.Text.Encoding]::ASCII.GetString($bytes, 0, [Math]::Min(16, $bytes.Length))
    if (-not $header.StartsWith("SQLite format 3")) {
        Fail "SQLite header is missing."
    }

    if (-not (Test-Path $SqliteAssemblyPath)) {
        Fail "Microsoft.Data.Sqlite.dll not found. Provide -SqliteAssemblyPath from a published API folder."
    }

    Add-Type -Path (Resolve-Path $SqliteAssemblyPath)
    $connection = New-Object Microsoft.Data.Sqlite.SqliteConnection("Data Source=$copyPath;Mode=ReadOnly;Pooling=False")
    $connection.Open()

    try {
        $command = $connection.CreateCommand()
        $command.CommandText = @"
SELECT COUNT(*)
FROM sqlite_master
WHERE type = 'table'
  AND name IN ('documents', 'Users', 'Roles', 'document_categories', 'document_statuses');
"@
        $count = [int]$command.ExecuteScalar()
        if ($count -lt 5) {
            Fail "Required core tables are missing."
        }

        $integrityCommand = $connection.CreateCommand()
        $integrityCommand.CommandText = "PRAGMA integrity_check;"
        $integrity = [string]$integrityCommand.ExecuteScalar()
        if ($integrity -ne "ok") {
            Fail "SQLite integrity_check failed: $integrity"
        }
    }
    finally {
        $connection.Dispose()
    }

    Pass "Backup restore verification passed: $BackupPath"
}
finally {
    if (Test-Path $copyPath) {
        Remove-Item -LiteralPath $copyPath -Force
    }
}

exit 0
