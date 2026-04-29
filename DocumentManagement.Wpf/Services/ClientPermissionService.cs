namespace DocumentManagement.Wpf.Services;

public class ClientPermissionService
{
    private readonly ApiAuthService _authService;

    public ClientPermissionService(ApiAuthService authService)
    {
        _authService = authService;
    }

    private string CurrentRole =>
        _authService.CurrentUser?.Role?.Trim() ?? string.Empty;

    public bool IsAdmin =>
        string.Equals(CurrentRole, "Admin", StringComparison.OrdinalIgnoreCase);

    public bool IsManager =>
        string.Equals(CurrentRole, "Manager", StringComparison.OrdinalIgnoreCase);

    public bool IsPublisher =>
        string.Equals(CurrentRole, "Publisher", StringComparison.OrdinalIgnoreCase);

    public bool IsStaff =>
        string.Equals(CurrentRole, "Staff", StringComparison.OrdinalIgnoreCase);

    public bool CanViewDashboard()
    {
        return IsAdmin || IsManager || IsPublisher || IsStaff;
    }

    public bool CanViewDocuments()
    {
        return IsAdmin || IsManager || IsPublisher || IsStaff;
    }

    public bool CanCreateDocuments()
    {
        return IsAdmin || IsManager || IsPublisher || IsStaff;
    }

    public bool CanEditDocuments()
    {
        return IsAdmin || IsManager || IsPublisher;
    }

    public bool CanDeleteDocuments()
    {
        return IsAdmin;
    }

    public bool CanExportDocuments()
    {
        return IsAdmin || IsManager || IsPublisher;
    }

    public bool CanBackup()
    {
        return IsAdmin;
    }

    public bool CanRestore()
    {
        return IsAdmin;
    }
}
