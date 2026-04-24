using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Security;
using DocumentManagement.Wpf.Commands;
using DocumentManagement.Wpf.Services;
using DocumentManagement.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace DocumentManagement.Wpf.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAuthService _authService;
    private readonly IPermissionService _permissionService;
    private readonly INotificationService _notificationService;
    private readonly IConfirmDialogService _confirmDialogService;

    private BaseViewModel? _currentView;
    private bool _isSystemOperationRunning;

    public BaseViewModel? CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    public bool IsSystemOperationRunning
    {
        get => _isSystemOperationRunning;
        set
        {
            if (SetProperty(ref _isSystemOperationRunning, value))
            {
                OnPropertyChanged(nameof(CanRunBackupCommand));
                OnPropertyChanged(nameof(CanRunRestoreCommand));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string CurrentDisplayName =>
        _authService.CurrentUser?.DisplayName ?? "Chưa đăng nhập";

    public string CurrentRoleName =>
        _authService.CurrentUser?.Roles.FirstOrDefault() ?? "Không có quyền";

    public bool CanViewDashboard =>
        _permissionService.HasPermission(_authService.CurrentUser, PermissionCodes.DashboardView);

    public bool CanViewDocuments =>
        _permissionService.HasPermission(_authService.CurrentUser, PermissionCodes.DocumentView);

    public bool CanViewTasks =>
        _permissionService.HasPermission(_authService.CurrentUser, PermissionCodes.TaskView);

    public bool CanViewReports =>
        _permissionService.HasPermission(_authService.CurrentUser, PermissionCodes.ReportView);

    public bool CanViewSettings =>
        _permissionService.HasPermission(_authService.CurrentUser, PermissionCodes.SettingsView);

    public bool CanBackup =>
        _permissionService.HasPermission(_authService.CurrentUser, PermissionCodes.BackupCreate);

    public bool CanRestore =>
        _permissionService.HasPermission(_authService.CurrentUser, PermissionCodes.BackupRestore);

    public bool CanRunBackupCommand => CanBackup && !IsSystemOperationRunning;

    public bool CanRunRestoreCommand => CanRestore && !IsSystemOperationRunning;

    public bool CanCreateDocument =>
        _permissionService.HasPermission(_authService.CurrentUser, PermissionCodes.DocumentCreate);

    public bool CanEditDocument =>
        _permissionService.HasPermission(_authService.CurrentUser, PermissionCodes.DocumentEdit);

    public ICommand ShowDashboardCommand { get; }
    public ICommand ShowDocumentListCommand { get; }
    public ICommand CreateDocumentCommand { get; }
    public ICommand BackupCommand { get; }
    public ICommand RestoreCommand { get; }

    public MainViewModel(
        IServiceProvider serviceProvider,
        IAuthService authService,
        IPermissionService permissionService,
        INotificationService notificationService,
        IConfirmDialogService confirmDialogService)
    {
        _serviceProvider = serviceProvider;
        _authService = authService;
        _permissionService = permissionService;
        _notificationService = notificationService;
        _confirmDialogService = confirmDialogService;

        ShowDashboardCommand = new RelayCommand(_ => ShowDashboard(), _ => CanViewDashboard);
        ShowDocumentListCommand = new RelayCommand(_ => ShowDocuments(), _ => CanViewDocuments);
        CreateDocumentCommand = new RelayCommand(async _ => await CreateDocumentAsync(), _ => CanCreateDocument);
        BackupCommand = new RelayCommand(async _ => await RunBackupAsync(), _ => CanRunBackupCommand);
        RestoreCommand = new RelayCommand(async _ => await RunRestoreAsync(), _ => CanRunRestoreCommand);

        if (CanViewDashboard)
        {
            ShowDashboard();
        }
        else if (CanViewDocuments)
        {
            ShowDocuments();
        }
    }

    public void RefreshPermissions()
    {
        OnPropertyChanged(nameof(CurrentDisplayName));
        OnPropertyChanged(nameof(CurrentRoleName));
        OnPropertyChanged(nameof(CanViewDashboard));
        OnPropertyChanged(nameof(CanViewDocuments));
        OnPropertyChanged(nameof(CanViewTasks));
        OnPropertyChanged(nameof(CanViewReports));
        OnPropertyChanged(nameof(CanViewSettings));
        OnPropertyChanged(nameof(CanBackup));
        OnPropertyChanged(nameof(CanRestore));
        OnPropertyChanged(nameof(CanRunBackupCommand));
        OnPropertyChanged(nameof(CanRunRestoreCommand));
        OnPropertyChanged(nameof(CanCreateDocument));
        OnPropertyChanged(nameof(CanEditDocument));

        CommandManager.InvalidateRequerySuggested();
    }

    public void ShowDashboard()
    {
        if (!CanViewDashboard)
        {
            _notificationService.ShowWarning(
                "Bạn không có quyền truy cập Bảng điều khiển.",
                "Từ chối truy cập");
            return;
        }

        try
        {
            var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
            _ = vm.LoadAsync();
            CurrentView = vm;
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                $"Không thể mở dashboard: {ex.Message}",
                "Lỗi");
        }
    }

    public void ShowDocuments()
    {
        if (!CanViewDocuments)
        {
            _notificationService.ShowWarning(
                "Bạn không có quyền truy cập Quản lý tài liệu.",
                "Từ chối truy cập");
            return;
        }

        try
        {
            var vm = _serviceProvider.GetRequiredService<DocumentListViewModel>();
            _ = vm.LoadAsync();
            CurrentView = vm;
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                $"Không thể mở danh sách văn bản: {ex.Message}",
                "Lỗi");
        }
    }

    public async Task OpenDocumentAsync(long documentId)
    {
        if (!CanViewDocuments)
        {
            _notificationService.ShowWarning(
                "Bạn không có quyền xem văn bản.",
                "Từ chối truy cập");
            return;
        }

        if (documentId <= 0)
        {
            _notificationService.ShowWarning(
                "Mã văn bản không hợp lệ.",
                "Thông báo");
            return;
        }

        try
        {
            var vm = _serviceProvider.GetRequiredService<DocumentFormViewModel>();
            await vm.LoadDocumentAsync(documentId);

            vm.ApplyAccessMode(isReadOnly: !CanEditDocument);

            var window = new DocumentFormWindow(vm)
            {
                Owner = global::System.Windows.Application.Current?.MainWindow
            };

            var dialogResult = window.ShowDialog();

            if (dialogResult == true)
            {
                await RefreshCurrentViewAsync();

                _notificationService.ShowSuccess(
                    "Dữ liệu văn bản đã được cập nhật.",
                    "Cập nhật thành công");
            }
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                $"Không thể mở văn bản: {ex.Message}",
                "Lỗi");
        }
    }

    private async Task CreateDocumentAsync()
    {
        if (!CanCreateDocument)
        {
            _notificationService.ShowWarning(
                "Bạn không có quyền tạo mới văn bản.",
                "Từ chối thao tác");
            return;
        }

        try
        {
            var vm = _serviceProvider.GetRequiredService<DocumentFormViewModel>();
            vm.ApplyAccessMode(isReadOnly: false);

            var window = new DocumentFormWindow(vm)
            {
                Owner = global::System.Windows.Application.Current?.MainWindow
            };

            var dialogResult = window.ShowDialog();

            if (dialogResult == true)
            {
                await RefreshCurrentViewAsync();

                _notificationService.ShowSuccess(
                    "Văn bản mới đã được lưu.",
                    "Tạo văn bản thành công");
            }
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                $"Không thể mở form tạo văn bản: {ex.Message}",
                "Lỗi");
        }
    }

    private async Task RefreshCurrentViewAsync()
    {
        if (CurrentView is DashboardViewModel dashboardVm)
        {
            await dashboardVm.LoadAsync();
            return;
        }

        if (CurrentView is DocumentListViewModel listVm)
        {
            await listVm.LoadAsync();
        }
    }

    private async Task RunBackupAsync()
    {
        if (!CanBackup)
        {
            _notificationService.ShowWarning(
                "Bạn không có quyền sao lưu dữ liệu.",
                "Từ chối thao tác");
            return;
        }

        if (IsSystemOperationRunning)
        {
            _notificationService.ShowInfo(
                "Hệ thống đang thực hiện thao tác khác. Vui lòng chờ.",
                "Đang xử lý");
            return;
        }

        try
        {
            IsSystemOperationRunning = true;

            var backupFolder = GetBackupFolder();
            var dbPath = GetDatabasePath();
            var storageRoot = GetStorageRoot();

            Directory.CreateDirectory(backupFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            Directory.CreateDirectory(storageRoot);

            if (!File.Exists(dbPath))
            {
                _notificationService.ShowWarning(
                    $"Không tìm thấy database: {dbPath}",
                    "Không thể sao lưu");
                return;
            }

            var backupService = _serviceProvider.GetRequiredService<IBackupService>();
            var backupPath = await backupService.CreateBackupAsync(dbPath, storageRoot, backupFolder);

            _notificationService.ShowSuccess(
                $"Sao lưu thành công: {Path.GetFileName(backupPath)}",
                "Sao lưu dữ liệu");

            var shouldOpenFolder = _confirmDialogService.Confirm(
                $"Sao lưu thành công:\n{Path.GetFileName(backupPath)}\n\nBạn có muốn mở thư mục sao lưu không?",
                "Sao lưu dữ liệu",
                "Mở thư mục",
                "Đóng",
                ConfirmDialogType.Info);

            if (shouldOpenFolder)
            {
                OpenFolder(backupFolder);
            }
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                $"Lỗi sao lưu: {ex.Message}",
                "Lỗi");
        }
        finally
        {
            IsSystemOperationRunning = false;
        }
    }

    private async Task RunRestoreAsync()
    {
        if (!CanRestore)
        {
            _notificationService.ShowWarning(
                "Bạn không có quyền khôi phục dữ liệu.",
                "Từ chối thao tác");
            return;
        }

        if (IsSystemOperationRunning)
        {
            _notificationService.ShowInfo(
                "Hệ thống đang thực hiện thao tác khác. Vui lòng chờ.",
                "Đang xử lý");
            return;
        }

        try
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Backup files (*.zip)|*.zip",
                Title = "Chọn file backup để khôi phục",
                InitialDirectory = GetBackupFolder(),
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var firstConfirm = _confirmDialogService.Confirm(
                "Khôi phục sẽ ghi đè database và toàn bộ thư mục storage hiện tại.\n\nHệ thống sẽ tự tạo một bản backup an toàn trước khi khôi phục.\n\nBạn có chắc chắn muốn tiếp tục?",
                "Xác nhận khôi phục",
                "Tiếp tục",
                "Hủy",
                ConfirmDialogType.Warning);

            if (!firstConfirm)
            {
                return;
            }

            var secondConfirm = _confirmDialogService.Confirm(
                "Xác nhận lần cuối:\n\nSau khi khôi phục thành công, ứng dụng sẽ đóng. Bạn cần mở lại ứng dụng để tiếp tục.",
                "Xác nhận lần cuối",
                "Khôi phục",
                "Hủy",
                ConfirmDialogType.Danger);

            if (!secondConfirm)
            {
                return;
            }

            IsSystemOperationRunning = true;

            var dbPath = GetDatabasePath();
            var storageRoot = GetStorageRoot();

            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            Directory.CreateDirectory(storageRoot);

            var backupService = _serviceProvider.GetRequiredService<IBackupService>();
            await backupService.RestoreBackupAsync(dialog.FileName, dbPath, storageRoot);

            _confirmDialogService.Confirm(
                "Khôi phục thành công.\n\nỨng dụng sẽ đóng. Vui lòng mở lại ứng dụng để tiếp tục.",
                "Khôi phục dữ liệu",
                "Đóng ứng dụng",
                "Đóng",
                ConfirmDialogType.Info);

            global::System.Windows.Application.Current?.Shutdown();
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                $"Lỗi khôi phục: {ex.Message}",
                "Lỗi");
        }
        finally
        {
            IsSystemOperationRunning = false;
        }
    }

    private static string GetBackupFolder()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "DocumentManagement",
            "Backups");
    }

    private static string GetDatabasePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "database", "app.db");
    }

    private static string GetStorageRoot()
    {
        return Path.Combine(AppContext.BaseDirectory, "storage");
    }

    private static void OpenFolder(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }
}