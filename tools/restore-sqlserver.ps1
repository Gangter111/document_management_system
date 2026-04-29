param(
    [string]$ConnectionString = "Server=localhost\SQLEXPRESS;Database=DocumentManagementDb;Trusted_Connection=True;TrustServerCertificate=True",
    [Parameter(Mandatory = $true)]
    [string]$BackupPath,
    [string]$ServiceName = "DocumentManagement.Api"
)

$ErrorActionPreference = "Stop"

function Info($message) { Write-Host "[INFO] $message" -ForegroundColor Cyan }
function Pass($message) { Write-Host "[PASS] $message" -ForegroundColor Green }
function Fail($message) { Write-Host "[FAIL] $message" -ForegroundColor Red; exit 1 }

if (-not (Test-Path $BackupPath)) {
    Fail "Backup file not found: $BackupPath"
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    Fail "ConnectionString is required."
}

$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $ConnectionString
$databaseName = $builder.InitialCatalog

if ([string]::IsNullOrWhiteSpace($databaseName)) {
    Fail "ConnectionString must include Database or Initial Catalog."
}

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service -and $service.Status -ne "Stopped") {
    Info "Stopping service $ServiceName"
    Stop-Service -Name $ServiceName -Force
}

$builder.InitialCatalog = "master"
$sql = @"
DECLARE @databaseName sysname = @DatabaseName;
DECLARE @backupPath nvarchar(4000) = @BackupPath;
DECLARE @statement nvarchar(max);

IF DB_ID(@databaseName) IS NOT NULL
BEGIN
    SET @statement = N'ALTER DATABASE ' + QUOTENAME(@databaseName) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE;';
    EXEC sys.sp_executesql @statement;
END

SET @statement =
    N'RESTORE DATABASE ' + QUOTENAME(@databaseName) +
    N' FROM DISK = ' + QUOTENAME(@backupPath, '''') +
    N' WITH REPLACE, RECOVERY, CHECKSUM;';
EXEC sys.sp_executesql @statement;

SET @statement = N'ALTER DATABASE ' + QUOTENAME(@databaseName) + N' SET MULTI_USER;';
EXEC sys.sp_executesql @statement;
"@

$connection = New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
$command = $connection.CreateCommand()
$command.CommandTimeout = 0
$command.CommandText = $sql
$null = $command.Parameters.Add("@DatabaseName", [System.Data.SqlDbType]::NVarChar, 128)
$command.Parameters["@DatabaseName"].Value = $databaseName
$null = $command.Parameters.Add("@BackupPath", [System.Data.SqlDbType]::NVarChar, 4000)
$command.Parameters["@BackupPath"].Value = (Resolve-Path $BackupPath).Path

try {
    Info "Database: $databaseName"
    Info "Backup file: $BackupPath"
    $connection.Open()
    $command.ExecuteNonQuery() | Out-Null
}
finally {
    $connection.Dispose()
}

if ($service) {
    Info "Starting service $ServiceName"
    Start-Service -Name $ServiceName
}

Pass "SQL Server restore completed."
exit 0
