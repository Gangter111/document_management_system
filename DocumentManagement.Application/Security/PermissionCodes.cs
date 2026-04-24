namespace DocumentManagement.Application.Security;

public static class PermissionCodes
{
    public const string DashboardView = "dashboard.view";

    public const string DocumentView = "document.view";
    public const string DocumentCreate = "document.create";
    public const string DocumentEdit = "document.edit";
    public const string DocumentDelete = "document.delete";

    // Workflow permissions
    public const string DocumentSubmit = "document.submit";
    public const string DocumentApprove = "document.approve";
    public const string DocumentReject = "document.reject";

    public const string TaskView = "task.view";
    public const string TaskManage = "task.manage";

    public const string ReportView = "report.view";

    public const string SettingsView = "settings.view";

    public const string BackupCreate = "backup.create";
    public const string BackupRestore = "backup.restore";
}