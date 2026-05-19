using System.Security.Cryptography;
using DocumentManagement.Application.Interfaces;

namespace DocumentManagement.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private const long MaxFileSizeBytes = 100L * 1024L * 1024L;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".doc",
        ".docx",
        ".xls",
        ".xlsx",
        ".png",
        ".jpg",
        ".jpeg"
    };

    private readonly string _rootFolder;
    private readonly string _quarantineFolder;

    public LocalFileStorageService(string rootFolder)
    {
        _rootFolder = Path.GetFullPath(rootFolder);
        _quarantineFolder = Path.Combine(_rootFolder, ".quarantine");
        Directory.CreateDirectory(_rootFolder);
        Directory.CreateDirectory(_quarantineFolder);
    }

    public async Task<(string StoredFileName, string StoredFilePath, long FileSize, string FileHash)> SaveFileAsync(
        string sourceFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("Source file was not found.", sourceFilePath);

        var sourceFullPath = Path.GetFullPath(sourceFilePath);
        var extension = Path.GetExtension(sourceFullPath).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("File type is not allowed.");

        var fileInfo = new FileInfo(sourceFullPath);
        if (fileInfo.Length <= 0)
            throw new InvalidOperationException("File is empty.");

        if (fileInfo.Length > MaxFileSizeBytes)
            throw new InvalidOperationException("File exceeds the 100 MB storage limit.");

        await ValidateFileSignatureAsync(sourceFullPath, extension, cancellationToken);

        var quarantinePath = Path.Combine(_quarantineFolder, $"{Guid.NewGuid():N}{extension}");
        string hash;

        try
        {
            hash = await CopyToQuarantineAndHashAsync(sourceFullPath, quarantinePath, cancellationToken);
        }
        catch
        {
            SafeDeleteFile(quarantinePath);
            throw;
        }

        var storedFileName = $"{hash}{extension}";
        var targetDirectory = Path.Combine(_rootFolder, hash[..2], hash.Substring(2, 2));
        Directory.CreateDirectory(targetDirectory);

        var targetPath = Path.Combine(targetDirectory, storedFileName);
        EnsurePathIsInsideRoot(targetPath);

        try
        {
            if (!File.Exists(targetPath))
            {
                File.Move(quarantinePath, targetPath);
            }
        }
        finally
        {
            SafeDeleteFile(quarantinePath);
        }

        return (storedFileName, targetPath, fileInfo.Length, hash);
    }

    public bool DeleteFile(string storedFilePath)
    {
        if (string.IsNullOrWhiteSpace(storedFilePath))
            return false;

        var fullPath = Path.GetFullPath(storedFilePath);
        EnsurePathIsInsideRoot(fullPath);

        if (!File.Exists(fullPath))
            return false;

        File.Delete(fullPath);
        return true;
    }

    public Task<IReadOnlyList<string>> ListStoredFilesAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_rootFolder))
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        var files = Directory
            .EnumerateFiles(_rootFolder, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetFullPath(path).StartsWith(_quarantineFolder, StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .ToList();

        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    private static async Task<string> CopyToQuarantineAndHashAsync(
        string sourceFilePath,
        string quarantinePath,
        CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        await using var sourceStream = new FileStream(
            sourceFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var targetStream = new FileStream(
            quarantinePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buffer = new byte[81920];
        int bytesRead;

        while ((bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            await targetStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
        }

        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        await targetStream.FlushAsync(cancellationToken);

        return Convert.ToHexString(sha256.Hash ?? throw new InvalidOperationException("SHA-256 calculation failed."));
    }

    private static async Task ValidateFileSignatureAsync(
        string filePath,
        string extension,
        CancellationToken cancellationToken)
    {
        var header = new byte[8];
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: header.Length,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        var valid = extension switch
        {
            ".pdf" => read >= 4 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46,
            ".png" => read >= 8 && header.SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".jpg" or ".jpeg" => read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".docx" or ".xlsx" => read >= 4 && header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04,
            ".doc" or ".xls" => read >= 8 && header.SequenceEqual(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }),
            _ => false
        };

        if (!valid)
            throw new InvalidOperationException("File content does not match its declared type.");
    }

    private void EnsurePathIsInsideRoot(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = _rootFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("File path is outside the configured storage root.");
    }

    private static void SafeDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort cleanup for quarantined files.
        }
    }
}
