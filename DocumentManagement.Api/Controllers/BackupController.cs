using DocumentManagement.Api.Security;
using DocumentManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace DocumentManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class BackupController : ControllerBase
{
    private static readonly SemaphoreSlim BackupRestoreLock = new(1, 1);

    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public BackupController(
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    [HttpGet("download")]
    public async Task<IActionResult> Download(CancellationToken cancellationToken)
    {
        if (!User.IsAdmin())
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Bạn không có quyền sao lưu dữ liệu.");
        }

        if (!IsSqliteProvider())
        {
            return BadRequest("Backup trực tiếp qua API chỉ hỗ trợ SQLite. SQL Server cần dùng maintenance job hoặc script backup riêng.");
        }

        var databasePath = ResolveDatabasePath();

        if (!System.IO.File.Exists(databasePath))
        {
            return NotFound($"Không tìm thấy database: {databasePath}");
        }

        await BackupRestoreLock.WaitAsync(cancellationToken);

        try
        {
            var backupDir = Path.Combine(_environment.ContentRootPath, "backups");
            Directory.CreateDirectory(backupDir);

            var fileName = $"document_management_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
            var backupPath = Path.Combine(backupDir, fileName);

            await CreateSqliteBackupAsync(databasePath, backupPath, cancellationToken);

            return PhysicalFile(
                backupPath,
                "application/octet-stream",
                fileName);
        }
        finally
        {
            BackupRestoreLock.Release();
        }
    }

    [HttpPost("restore")]
    [RequestSizeLimit(200_000_000)]
    public async Task<IActionResult> Restore(IFormFile file, CancellationToken cancellationToken)
    {
        if (!User.IsAdmin())
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Chỉ Admin được khôi phục dữ liệu.");
        }

        if (!IsSqliteProvider())
        {
            return BadRequest("Restore trực tiếp qua API chỉ hỗ trợ SQLite. SQL Server cần dùng quy trình restore riêng trên máy chủ database.");
        }

        if (file == null || file.Length <= 0)
        {
            return BadRequest("File khôi phục không hợp lệ.");
        }

        var extension = Path.GetExtension(file.FileName);

        if (!string.Equals(extension, ".db", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Chỉ hỗ trợ file .db hoặc .sqlite.");
        }

        var databasePath = ResolveDatabasePath();
        var databaseDir = Path.GetDirectoryName(databasePath);

        if (string.IsNullOrWhiteSpace(databaseDir))
        {
            return BadRequest("Không xác định được thư mục database.");
        }

        Directory.CreateDirectory(databaseDir);

        var tempDir = Path.Combine(_environment.ContentRootPath, "restore-temp");
        Directory.CreateDirectory(tempDir);

        var uploadedPath = Path.Combine(tempDir, $"restore_{Guid.NewGuid():N}{extension}");

        await using (var stream = System.IO.File.Create(uploadedPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        await BackupRestoreLock.WaitAsync(cancellationToken);

        try
        {
            await ValidateSqliteDatabaseAsync(uploadedPath, cancellationToken);

            var safetyBackupPath = Path.Combine(
                databaseDir,
                $"document_management_before_restore_{DateTime.Now:yyyyMMdd_HHmmss}.db");

            if (System.IO.File.Exists(databasePath))
            {
                await CreateSqliteBackupAsync(databasePath, safetyBackupPath, cancellationToken);
            }

            SqliteConnection.ClearAllPools();

            System.IO.File.Copy(uploadedPath, databasePath, overwrite: true);

            DeleteIfExists(databasePath + "-wal");
            DeleteIfExists(databasePath + "-shm");

            return Ok(new
            {
                success = true,
                message = "Khôi phục dữ liệu thành công.",
                safetyBackup = safetyBackupPath
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        finally
        {
            DeleteIfExists(uploadedPath);
            BackupRestoreLock.Release();
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        if (!User.IsAdmin())
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Chỉ Admin được xem trạng thái backup.");
        }

        if (!IsSqliteProvider())
        {
            return Ok(new
            {
                provider = DatabaseProvider.SqlServer.ToString(),
                backupMode = "external"
            });
        }

        var databasePath = ResolveDatabasePath();

        return Ok(new
        {
            provider = DatabaseProvider.Sqlite.ToString(),
            exists = System.IO.File.Exists(databasePath)
        });
    }

    private string ResolveDatabasePath()
    {
        var databasePath = _configuration["Database:Path"];
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            databasePath = Path.Combine("database", "app.db");
        }

        if (Path.IsPathRooted(databasePath))
        {
            return databasePath;
        }

        var contentRootPath = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, databasePath));

        if (System.IO.File.Exists(contentRootPath))
        {
            return contentRootPath;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, databasePath));
    }

    private bool IsSqliteProvider()
    {
        var provider = _configuration.GetSection("Database").Get<DatabaseOptions>()?.GetProvider()
                       ?? DatabaseProvider.Sqlite;

        return provider == DatabaseProvider.Sqlite;
    }

    private static async Task CreateSqliteBackupAsync(string sourceDatabasePath, string backupPath, CancellationToken cancellationToken = default)
    {
        DeleteIfExists(backupPath);

        var backupDirectory = Path.GetDirectoryName(backupPath);

        if (!string.IsNullOrWhiteSpace(backupDirectory))
        {
            Directory.CreateDirectory(backupDirectory);
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = sourceDatabasePath
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"VACUUM INTO '{backupPath.Replace("'", "''")}';";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ValidateSqliteDatabaseAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText = @"
SELECT COUNT(*)
FROM sqlite_master
WHERE type = 'table'
  AND name IN ('documents', 'Users', 'Roles', 'document_categories', 'document_statuses');";

        var result = await command.ExecuteScalarAsync(cancellationToken);
        var count = Convert.ToInt32(result ?? 0);

        if (count < 5)
        {
          