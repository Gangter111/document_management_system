namespace DocumentManagement.Application.Models;

public class AppConfig
{
    public string DatabaseName { get; set; } = "app.db";
    public string AttachmentFolderName { get; set; } = "storage/attachments";
    public string BackupFolderName { get; set; } = "storage/backups";
    public string TempFolderName { get; set; } = "storage/temp";
    public int MaxUploadFileSizeMb { get; set; } = 50;
    public bool EnableOcr { get; set; } = true;
    public int ExpiryWarningDays { get; set; } = 7;
}
