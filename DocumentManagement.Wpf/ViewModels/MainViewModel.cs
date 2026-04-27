using System.IO;
using System.Windows.Input;
using DocumentManagement.Wpf.Commands;
using DocumentManagement.Wpf.Services;
using DocumentManagement.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace DocumentManagement.Wpf.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ApiService _apiService;
    private readonly ApiAuthService _authService;
    private readonly ClientPermissionService _permissionService;
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

    public string CurrentDisplayName
    {
        get
        {
            var fullName = _authService.CurrentUser?.FullName;
            var username = _authService.CurrentUser?.Username;

            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName;
            }

            if (!string.IsNullOrWhiteSpace(username))
            {
                return username;
            }

            return "Người dùng";
        }
    }

    public string CurrentRoleName
    {
        get
        {
            var role = _authService.CurrentUser?.Role;

            return string.IsNullOrWhiteSpace(role)
                ? "User"
                : role;
        }
    }

    public bool CanViewDashboard => _permissionService.CanViewDashboard();

    public bool CanViewDocuments => _permissionService.CanViewDocuments();

    public bool CanViewTasks => false;

    public bool CanViewReports => false;

    public bool CanViewSettings => string.Equals(CurrentRoleName, "Admin", StringComparison.OrdinalIgnoreCase);

    public bool CanBackup =>
        string.Equals(CurrentRoleName, "Admin", StringComparison.OrdinalIgnoreCase)
        || string.Equals(CurrentRoleName, "Manager", StringComparison.OrdinalIgnoreCase);

    public bool CanRestore =>
        string.Equals(CurrentRoleName, "Admin", StringComparison.OrdinalIgnoreCase);

    public bool CanRunBackupCommand => CanBackup && !IsSystemOperationRunning;

    public bool CanRunRestoreCommand => CanRestore && !IsSystemOperationRunning;

    public bool CanCreateDocument => _permissionService.CanCreateDocuments();

    public bool CanEditDocument => _permissionService.CanEditDocuments();

    public ICommand ShowDashboardCommand { get; }

    public ICommand ShowDocumentListCommand { get; }

    public ICommand CreateDocumentCommand { get; }

    public ICommand BackupCommand { get; }

    public ICommand RestoreCommand { get; }

    public MainViewModel(
        IServiceProvider serviceProvider,
        ApiService apiService,
        ApiAuthService authService,
        ClientPermissionService permissionService,
        INotificationService notificationService,
        IConfirmDialogService confirmDialogService)
    {
        _serviceProvider = serviceProvider;
        _apiService = apiService;
        _authService = authService;
        _permissionService = permissionService;
        _notificationService = notificationService;
        _confirmDialogService = confirmDialogService;

        ShowDashboardCommand = new RelayCommand(_ => ShowDashboard(), _ => CanViewDashboard);
        ShowDocumentListCommand = new RelayCommand(_ => ShowDocuments(), _ => CanViewDocuments);
        CreateDocumentCommand = new RelayCommand(async _ => await CreateDocumentAsync(), _ => CanCreateDocument);

        BackupCommand = new RelayCommand(
            async _ => await BackupAsync(),
            _ => CanRunBackupCommand);

        RestoreCommand = new RelayCommand(
            async _ => await RestoreAsync(),
            _ => CanRunRestoreCommand);

        RefreshPermissions();

        if (CanViewDashboard)
        {
            ShowDashboard();
        }
        else
        {
            ShowDocuments();
        }
    }

    public void RefreshUserInfo()
    {
        OnPropertyChanged(nameof(CurrentDisplayName));
        OnPropertyChanged(nameof(CurrentRoleName));
    }

    public void RefreshPermissions()
    {
        RefreshUserInfo();

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
                "Bạn không có quyền xem bảng điều khiển.",
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
                "Bạn không có quyền xem danh sách văn bản.",
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
        if (documentId <= 0)
        {
            _notificationService.ShowWarning(
                "Mã văn bản không hợp lệ.",
                "Thông báo");
            return;
        }

        if (!CanViewDocuments)
        {
            _notificationService.ShowWarning(
                "Bạn không có quyền xem văn bản.",
                "Từ chối truy cập");
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
                "Bạn không có quyền tạo văn bản.",
                "Từ chối thao tác");
            return;
        }

        try
        {
            var vm = _serviceProvider.GetRequiredService<DocumentFormViewModel>();
            vm.ResetForCreate();
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

    private async Task BackupAsync()
    {
        if (!CanBackup)
        {
            _notificationService.ShowWarning(
                "Bạn không có quyền sao lưu dữ liệu.",
                "Từ chối thao tác");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Lưu file sao lưu dữ liệu",
            Filter = "SQLite Database (*.db)|*.db|All Files (*.*)|*.*",
            FileName = $"document_management_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            IsSystemOperationRunning = true;

            var bytes = await _apiService.DownloadBackupAsync();
            await File.WriteAllBytesAsync(dialog.FileName, bytes);

            _notificationService.ShowSuccess(
                "Đã sao lưu dữ liệu thành công.",
                "Sao lưu dữ liệu");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                $"Sao lưu thất bại: {ex.Message}",
                "Lỗi sao lưu");
        }
        finally
        {
            IsSystemOperationRunning = false;
        }
    }

    private async Task RestoreAsync()
    {
        if (!CanRestore)
        {
            _notificationService.ShowWarning(
                "Chỉ Admin được khôi phục dữ liệu.",
                "Từ chối thao tác");
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Chọn file khôi phục dữ liệu",
            Filter = "SQLite Database (*.db;*.sqlite)|*.db;*.sqlite|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var confirmed = _confirmDialogService.Confirm(
            "Khôi phục dữ liệu sẽ ghi đè database hiện tại. Hệ thống sẽ tự tạo bản sao an toàn trước khi khôi phục. Tiếp tục?",
            "Xác nhận khôi phục dữ liệu",
            "Khôi phục",
            "Hủy",
            ConfirmDialogType.Danger);

        if (!confirmed)
        {
            return;
        }

        try
        {
            IsSystemOperationRunning = true;

            await _apiService.RestoreBackupAsync(dialog.FileName);
            await RefreshCurrentViewAsync();

            _notificationService.ShowSuccess(
                "Đã khôi phục dữ liệu thành công. Nên khởi động lại ứng dụng để làm sạch cache UI.",
                "Khôi phục dữ liệu");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                $"Khôi phục thất bại: {ex.Message}",
                "Lỗi khôi phục");
        }
        finally
        {
            IsSystemOperationRunning = false;
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
}