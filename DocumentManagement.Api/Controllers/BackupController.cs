using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using System.Text.RegularExpressions;

namespace DocumentManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackupController : ControllerBase
{
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
    public async Task<IActionResult> Download()
    {
        var role = GetRole();

        if (!IsAdmin(role) && !IsManager(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Bạn không có quyền sao lưu dữ liệu.");
        }

        var databasePath = ResolveDatabasePath();

        if (!System.IO.File.Exists(databasePath))
        {
            return NotFound($"Không tìm thấy database: {databasePath}");
        }

        var backupDir = Path.Combine(_environment.ContentRootPath, "backups");
        Directory.CreateDirectory(backupDir);

        var fileName = $"document_management_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
        var backupPath = Path.Combine(backupDir, fileName);

        await CreateSqliteBackupAsync(databasePath, backupPath);

        var bytes = await System.IO.File.ReadAllBytesAsync(backupPath);

        return File(
            bytes,
            "application/octet-stream",
            fileName);
    }

    [HttpPost("restore")]
    [RequestSizeLimit(200_000_000)]
    public async Task<IActionResult> Restore(IFormFile file)
    {
        var role = GetRole();

        if (!IsAdmin(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Chỉ Admin được khôi phục dữ liệu.");
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
            await file.CopyToAsync(stream);
        }

        try
        {
            await ValidateSqliteDatabaseAsync(uploadedPath);

            var safetyBackupPath = Path.Combine(
                databaseDir,
                $"document_management_before_restore_{DateTime.Now:yyyyMMdd_HHmmss}.db");

            if (System.IO.File.Exists(databasePath))
            {
                await CreateSqliteBackupAsync(databasePath, safetyBackupPath);
            }

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
        finally
        {
            DeleteIfExists(uploadedPath);
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        var databasePath = ResolveDatabasePath();

        return Ok(new
        {
            databasePath,
            exists = System.IO.File.Exists(databasePath)
        });
    }

    private string GetRole()
    {
        if (Request.Headers.TryGetValue("X-User-Role", out var roleHeader))
        {
            return roleHeader.ToString();
        }

        return string.Empty;
    }

    private static bool IsAdmin(string role)
    {
        return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsManager(string role)
    {
        return string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveDatabasePath()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Thiếu ConnectionStrings:DefaultConnection.");
        }

        var match = Regex.Match(
            connectionString,
            @"Data Source\s*=\s*(?<path>[^;]+)",
            RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            throw new InvalidOperationException("Connection string không có Data Source.");
        }

        var dataSource = match.Groups["path"].Value.Trim();

        if (Path.IsPathRooted(dataSource))
        {
            return dataSource;
        }

        var contentRootPath = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, dataSource));

        if (System.IO.File.Exists(contentRootPath))
        {
            return contentRootPath;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, dataSource));
    }

    private static async Task CreateSqliteBackupAsync(string sourceDatabasePath, string backupPath)
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
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"VACUUM INTO '{backupPath.Replace("'", "''")}';";

        await command.ExecuteNonQueryAsync();
    }

    private static async Task ValidateSqliteDatabaseAsync(string databasePath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();

        command.CommandText = @"
SELECT COUNT(*)
FROM sqlite_master
WHERE type = 'table'
  AND name = 'documents';";

        var result = await command.ExecuteScalarAsync();
        var count = Convert.ToInt32(result ?? 0);

        if (count <= 0)
        {
            throw new InvalidOperationException("File khôi phục không phải database hợp lệ của hệ thống.");
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
        }
    }
}