param(
    [string]$ConnectionString = "Server=localhost\SQLEXPRESS;Database=DocumentManagementDb;Trusted_Connection=True;TrustServerCertificate=True",
    [string]$BackupDirectory = "C:\QuanLyVanBan\Backups",
    [int]$RetentionDays = 30,
    [switch]$Compress,
    [switch]$CopyOnly
)

$ErrorActionPreference = "Stop"

function Info($message) { Write-Host "[INFO] $message" -ForegroundColor Cyan }
function Pass($message) { Write-Host "[PASS] $message" -ForegroundColor Green }
function Fail($message) { Write-Host "[FAIL] $message" -ForegroundColor Red; exit 1 }

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    Fail "ConnectionString is required."
}

if ([string]::IsNullOrWhiteSpace($BackupDirectory)) {
    Fail "BackupDirectory is required."
}

New-Item -ItemType Directory -Path $BackupDirectory -Force | Out-Null

$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $ConnectionString
$databaseName = $builder.InitialCatalog

if ([string]::IsNullOrWhiteSpace($databaseName)) {
    Fail "ConnectionString must include Database or Initial Catalog."
}

$builder.InitialCatalog = "master"
$safeDatabaseName = $databaseName -replace "[^a-zA-Z0-9_-]", "_"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupPath = Join-Path $BackupDirectory "$safeDatabaseName-$stamp.bak"

$backupOptions = @("INIT", "FORMAT", "CHECKSUM")
if ($Compress) {
    $backupOptions += "COMPRESSION"
}

if ($CopyOnly) {
    $backupOptions += "COPY_ONLY"
}

$backupOptionsClause = $backupOptions -join ", "
$sql = @"
DECLARE @databaseName sysname = @DatabaseName;
DECLARE @backupPath nvarchar(4000) = @BackupPath;
DECLARE @statement nvarchar(max) =
    N'BACKUP DATABASE ' + QUOTENAME(@databaseName) +
    N' TO DISK = ' + QUOTENAME(@backupPath, '''') +
    N' WITH $backupOptionsClause;';
EXEC sys.sp_executesql @statement;
"@

$connection = New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
$command = $connection.CreateCommand()
$command.CommandTimeout = 0
$command.CommandText = $sql
$null = $command.Parameters.Add("@DatabaseName", [System.Data.SqlDbType]::NVarChar, 128)
$command.Parameters["@DatabaseName"].Value = $databaseName
$null = $command.Parameters.Add("@BackupPath", [System.Data.SqlDbType]::NVarChar, 4000)
$command.Parameters["@BackupPath"].Value = $backupPath

try {
    Info "Database: $databaseName"
    Info "Backup path: $backupPath"
    $connection.Open()
    $command.ExecuteNonQuery() | Out-Null
}
finally {
    $connection.Dispose()
}

if ($RetentionDays -gt 0) {
    $cutoff = (Get-Date).AddDays(-$RetentionDays)
    Get-ChildItem -Path $BackupDirectory -Filter "$safeDatabaseName-*.bak" -File |
        Where-Object { $_.LastWriteTime -lt $cutoff } |
        Remove-Item -Force
}

Pass "SQL Server backup completed."
exit 0
