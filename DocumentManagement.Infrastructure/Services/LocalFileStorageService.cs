using DocumentManagement.Application.Interfaces;
using System.Security.Cryptography;

namespace DocumentManagement.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootFolder;

    public LocalFileStorageService(string rootFolder)
    {
        _rootFolder = rootFolder;
        Directory.CreateDirectory(_rootFolder);
    }

    public async Task<(string StoredFileName, string StoredFilePath, long FileSize, string FileHash)> SaveFileAsync(string sourceFilePath)
    {
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("Không tìm thấy file nguồn.", sourceFilePath);

        var extension = Path.GetExtension(sourceFilePath);
        var safeFileName = Path.GetFileNameWithoutExtension(sourceFilePath)
            .Replace(" ", "-")
            .Replace(".", "-");

        var storedFileName = $"{Guid.NewGuid()}_{safeFileName}{extension}";
        var year = DateTime.Now.Year.ToString();
        var month = DateTime.Now.Month.ToString("00");

        var targetDirectory = Path.Combine(_rootFolder, year, month);
        Directory.CreateDirectory(targetDirectory);

        var targetPath = Path.Combine(targetDirectory, storedFileName);

        await using (var sourceStream = File.OpenRead(sourceFilePath))
        await using (var targetStream = File.Create(targetPath))
        {
            await sourceStream.CopyToAsync(targetStream);
        }

        var fileInfo = new FileInfo(targetPath);
        var hash = ComputeSha256(targetPath);

        return (storedFileName, targetPath, fileInfo.Length, hash);
    }

    public bool DeleteFile(string storedFilePath)
    {
        if (!File.Exists(storedFilePath))
            return false;

        File.Delete(storedFilePath);
        return true;
    }

    private static string ComputeSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes);
    }
}
