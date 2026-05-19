namespace DocumentManagement.Api.Security;

public sealed record RuntimePaths(
    string DataRoot,
    string AttachmentsPath,
    string BackupPath,
    string LogFilePath);

public static class RuntimePathValidator
{
    public static RuntimePaths ResolveAndValidate(
        IConfiguration configuration,
        IHostEnvironment environment,
        string contentRootPath)
    {
        var dataRoot = configuration["Storage:DataRoot"];
        if (string.IsNullOrWhiteSpace(dataRoot))
        {
            dataRoot = environment.IsDevelopment()
                ? contentRootPath
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "QuanLyVanBan");
        }

        var attachmentsPath = ResolvePath(
            configuration["Storage:AttachmentsPath"],
            dataRoot,
            Path.Combine("storage", "attachments"));
        var backupPath = ResolvePath(
            configuration["Backup:Path"],
            dataRoot,
            "backups");
        var logFilePath = ResolvePath(
            configuration["Logging:FilePath"],
            dataRoot,
            Path.Combine("logs", "api-.log"));

        EnsureWritableDirectory(attachmentsPath, "attachment storage");
        EnsureWritableDirectory(backupPath, "backup");
        EnsureWritableDirectory(Path.GetDirectoryName(logFilePath) ?? dataRoot, "log");

        return new RuntimePaths(dataRoot, attachmentsPath, backupPath, logFilePath);
    }

    public static string ResolvePath(string? configuredPath, string basePath, string defaultRelativePath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? defaultRelativePath
            : configuredPath;

        return Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(basePath, path));
    }

    private static void EnsureWritableDirectory(string path, string label)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, $".write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Configured {label} path is not writable.", ex);
        }
    }
}
