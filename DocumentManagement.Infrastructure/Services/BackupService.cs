using DocumentManagement.Application.Interfaces;
using Microsoft.Data.Sqlite;
using System.IO.Compression;

namespace DocumentManagement.Infrastructure.Services;

public class BackupService : IBackupService
{
    private const string DatabaseFolderName = "database";
    private const string StorageFolderName = "storage";

    public async Task<string> CreateBackupAsync(string databasePath, string storageRoot, string backupFolder)
    {
        ValidatePath(databasePath, nameof(databasePath), "Đường dẫn database không hợp lệ.");
        ValidatePath(storageRoot, nameof(storageRoot), "Đường dẫn storage không hợp lệ.");
        ValidatePath(backupFolder, nameof(backupFolder), "Thư mục backup không hợp lệ.");

        if (!File.Exists(databasePath))
            throw new FileNotFoundException("Không tìm thấy file database.", databasePath);

        Directory.CreateDirectory(backupFolder);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(backupFolder, $"Backup_DocumentSystem_{timestamp}.zip");

        var tempRoot = Path.Combine(Path.GetTempPath(), $"DocumentManagement_Backup_{Guid.NewGuid():N}");
        var tempDatabaseFolder = Path.Combine(tempRoot, DatabaseFolderName);
        var tempStorageFolder = Path.Combine(tempRoot, StorageFolderName);

        try
        {
            Directory.CreateDirectory(tempDatabaseFolder);
            Directory.CreateDirectory(tempStorageFolder);

            SqliteConnection.ClearAllPools();

            var databaseFileName = Path.GetFileName(databasePath);
            var tempDatabasePath = Path.Combine(tempDatabaseFolder, databaseFileName);

            File.Copy(databasePath, tempDatabasePath, overwrite: true);

            if (Directory.Exists(storageRoot))
            {
                CopyDirectory(storageRoot, tempStorageFolder);
            }

            if (File.Exists(backupPath))
                File.Delete(backupPath);

            await Task.Run(() =>
            {
                ZipFile.CreateFromDirectory(
                    tempRoot,
                    backupPath,
                    CompressionLevel.Optimal,
                    includeBaseDirectory: false);
            });

            return backupPath;
        }
        finally
        {
            SafeDeleteDirectory(tempRoot);
        }
    }

    public async Task RestoreBackupAsync(string backupZipPath, string targetDatabasePath, string targetStorageRoot)
    {
        ValidatePath(backupZipPath, nameof(backupZipPath), "Đường dẫn file backup không hợp lệ.");
        ValidatePath(targetDatabasePath, nameof(targetDatabasePath), "Đường dẫn database đích không hợp lệ.");
        ValidatePath(targetStorageRoot, nameof(targetStorageRoot), "Đường dẫn storage đích không hợp lệ.");

        if (!File.Exists(backupZipPath))
            throw new FileNotFoundException("File sao lưu không tồn tại.", backupZipPath);

        if (!string.Equals(Path.GetExtension(backupZipPath), ".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("File khôi phục phải là định dạng .zip.");

        var targetDatabaseFolder = Path.GetDirectoryName(targetDatabasePath)
            ?? throw new InvalidOperationException("Không xác định được thư mục database đích.");

        Directory.CreateDirectory(targetDatabaseFolder);
        Directory.CreateDirectory(targetStorageRoot);

        var safetyBackupFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "DocumentManagement",
            "Backups",
            "SafetyBeforeRestore");

        if (File.Exists(targetDatabasePath))
        {
            await CreateBackupAsync(targetDatabasePath, targetStorageRoot, safetyBackupFolder);
        }

        SqliteConnection.ClearAllPools();

        var tempRoot = Path.Combine(Path.GetTempPath(), $"DocumentManagement_Restore_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempRoot);

            await Task.Run(() =>
            {
                ZipFile.ExtractToDirectory(backupZipPath, tempRoot, overwriteFiles: true);
            });

            var extractedDatabaseFolder = Path.Combine(tempRoot, DatabaseFolderName);
            var extractedStorageFolder = Path.Combine(tempRoot, StorageFolderName);

            if (!Directory.Exists(extractedDatabaseFolder))
                throw new InvalidOperationException("Gói backup không hợp lệ: thiếu thư mục database.");

            var expectedDatabaseName = Path.GetFileName(targetDatabasePath);
            var extractedDatabasePath = Path.Combine(extractedDatabaseFolder, expectedDatabaseName);

            if (!File.Exists(extractedDatabasePath))
            {
                var firstDb = Directory
                    .GetFiles(extractedDatabaseFolder, "*.db", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault();

                if (firstDb == null)
                    throw new FileNotFoundException("Không tìm thấy file database trong gói backup.");

                extractedDatabasePath = firstDb;
            }

            File.Copy(extractedDatabasePath, targetDatabasePath, overwrite: true);

            if (Directory.Exists(targetStorageRoot))
                Directory.Delete(targetStorageRoot, recursive: true);

            Directory.CreateDirectory(targetStorageRoot);

            if (Directory.Exists(extractedStorageFolder))
                CopyDirectory(extractedStorageFolder, targetStorageRoot);
        }
        finally
        {
            SafeDeleteDirectory(tempRoot);
            SqliteConnection.ClearAllPools();
        }
    }

    private static void ValidatePath(string path, string parameterName, string message)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException(message, parameterName);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var targetFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, targetFile, overwrite: true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var targetSubDir = Path.Combine(destinationDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, targetSubDir);
        }
    }

    private static void SafeDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Không làm fail nghiệp vụ chính chỉ vì lỗi cleanup temp folder.
        }
    }
}