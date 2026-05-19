using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using DocumentManagement.Contracts.Common;
using DocumentManagement.Contracts.Dashboard;
using DocumentManagement.Wpf.Commands;
using DocumentManagement.Wpf.Services;
using DocumentManagement.Wpf.Views;
using Microsoft.Extensions.Configuration;
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
    private readonly bool _isIntelligenceReviewEnabled;

    private BaseViewModel? _currentView;
    private bool _isSystemOperationRunning;

    public BaseViewModel? CurrentView
    {
        get => _currentView;
        set
        {
            if (ReferenceEquals(_currentView, value))
            {
                return;
            }

            if (_currentView is DocumentListViewModel documentListViewModel)
            {
                documentListViewModel.Deactivate();
            }

            if (_currentView is IDisposable disposable)
            {
                disposable.Dispose();
            }

            if (SetProperty(ref _currentView, value))
            {
                OnPropertyChanged(nameof(IsDashboardActive));
                OnPropertyChanged(nameof(IsCommandSearchVisible));
            }
        }
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
                RaiseSystemCommandState();
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

    public string CurrentDepartmentName
    {
        get
        {
            var department = _authService.CurrentUser?.Department;

            return string.IsNullOrWhiteSpace(department)
                ? "Chưa cấu hình phòng ban"
                : department;
        }
    }

    public bool CanViewDashboard => _permissionService.CanViewDashboard();

    public bool CanViewDocuments => _permissionService.CanViewDocuments();

    public bool CanViewTasks => false;

    public bool CanViewReports => true;

    public bool CanViewArchive => true;

    public bool CanViewCategories => true;

    public bool CanViewSettings => true;

    public bool IsDashboardActive => CurrentView is DashboardViewModel;

    public bool IsCommandSearchVisible => !IsDashboardActive;

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

    public ICommand ShowArchiveCommand { get; }

    public ICommand ShowReportsCommand { get; }

    public ICommand ShowCategoriesCommand { get; }

    public ICommand ShowSettingsCommand { get; }

    public ICommand OpenIntelligenceReviewCommand { get; }

    public ICommand BackupCommand { get; }

    public ICommand RestoreCommand { get; }

    public MainViewModel(
        IServiceProvider serviceProvider,
        ApiService apiService,
        ApiAuthService authService,
        ClientPermissionService permissionService,
        INotificationService notificationService,
        IConfirmDialogService confirmDialogService,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _apiService = apiService;
        _authService = authService;
        _permissionService = permissionService;
        _notificationService = notificationService;
        _confirmDialogService = confirmDialogService;
        _isIntelligenceReviewEnabled = configuration.GetValue("Features:IntelligenceReviewEnabled", false);

        ShowDashboardCommand = new RelayCommand(_ => ShowDashboard(), _ => CanViewDashboard);
        ShowDocumentListCommand = new RelayCommand(async _ => await ShowDocumentsAsync(), _ => CanViewDocuments);
        CreateDocumentCommand = new RelayCommand(async _ => await CreateDocumentAsync(), _ => CanCreateDocument);
        ShowArchiveCommand = new RelayCommand(async _ => await ShowArchiveAsync(), _ => CanViewArchive);
        ShowReportsCommand = new RelayCommand(async _ => await ShowReportsAsync(), _ => CanViewReports);
        ShowCategoriesCommand = new RelayCommand(async _ => await ShowCategoriesAsync(), _ => CanViewCategories);
        ShowSettingsCommand = new RelayCommand(_ => ShowSystemInfo(), _ => CanViewSettings);
        OpenIntelligenceReviewCommand = new RelayCommand(_ => OpenIntelligenceReview(), _ => _isIntelligenceReviewEnabled);

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
            _ = ShowDocumentsAsync();
        }
    }

    public void RefreshUserInfo()
    {
        OnPropertyChanged(nameof(CurrentDisplayName));
        OnPropertyChanged(nameof(CurrentRoleName));
        OnPropertyChanged(nameof(CurrentDepartmentName));
    }

    public void RefreshPermissions()
    {
        RefreshUserInfo();

        OnPropertyChanged(nameof(CanViewDashboard));
        OnPropertyChanged(nameof(CanViewDocuments));
        OnPropertyChanged(nameof(CanViewTasks));
        OnPropertyChanged(nameof(CanViewReports));
        OnPropertyChanged(nameof(CanViewArchive));
        OnPropertyChanged(nameof(CanViewCategories));
        OnPropertyChanged(nameof(CanViewSettings));
        OnPropertyChanged(nameof(IsDashboardActive));
        OnPropertyChanged(nameof(IsCommandSearchVisible));
        OnPropertyChanged(nameof(CanBackup));
        OnPropertyChanged(nameof(CanRestore));
        OnPropertyChanged(nameof(CanRunBackupCommand));
        OnPropertyChanged(nameof(CanRunRestoreCommand));
        OnPropertyChanged(nameof(CanCreateDocument));
        OnPropertyChanged(nameof(CanEditDocument));

        RaiseAllCommandState();
    }

    private void RaiseSystemCommandState()
    {
        (BackupCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RestoreCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void RaiseAllCommandState()
    {
        (ShowDashboardCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ShowDocumentListCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CreateDocumentCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ShowArchiveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ShowReportsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ShowCategoriesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ShowSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OpenIntelligenceReviewCommand as RelayCommand)?.RaiseCanExecuteChanged();
        RaiseSystemCommandState();
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

    public async Task ShowDocumentsAsync()
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
            CurrentView = vm;
            await vm.LoadAsync();
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                $"Không thể mở danh sách văn bản: {ex.Message}",
                "Lỗi");
        }
    }

    private async Task ShowArchiveAsync()
    {
        if (!CanViewArchive || !CanViewDocuments)
        {
            _notificationService.ShowWarning(
                "Ban khong co quyen xem kho luu tru.",
                "Tu choi truy cap");
            return;
        }

        try
        {
            var vm = _serviceProvider.GetRequiredService<DocumentListViewModel>();
            CurrentView = vm;
            vm.SelectQueueByCode("ARCHIVED");
            await vm.LoadAsync();
            vm.SelectQueueByCode("ARCHIVED");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                $"Khong the mo kho luu tru: {ex.Message}",
                "Loi");
        }
    }

    private async Task ShowReportsAsync()
    {
        try
        {
            var vm = new OperationalReportsViewModel();
            CurrentView = vm;

            var dashboard = await _apiService.GetDashboardAsync();
            vm.Apply(dashboard);
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                $"Khong the tai bao cao: {ex.Message}",
                "Loi bao cao");
        }
    }

    private async Task ShowCategoriesAsync()
    {
        try
        {
            var vm = new LookupCatalogViewModel();
            CurrentView = vm;

            var categories = await _apiService.GetCategoriesAsync();
            var statuses = await _apiService.GetStatusesAsync();
            vm.Apply(categories, statuses);
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                $"Khong the tai danh muc: {ex.Message}",
                "Loi danh muc");
        }
    }

    private void ShowSystemInfo()
    {
        CurrentView = new SystemInfoViewModel(
            CurrentDisplayName,
            CurrentRoleName,
            CurrentDepartmentName,
            _apiService.BaseUrl,
            CanBackup,
            CanRestore,
            BackupCommand,
            RestoreCommand,
            new RelayCommand(_ => OpenServerSettings()),
            new RelayCommand(async _ => await SeedDemoDataAsync(), _ => CanRestore),
            new RelayCommand(async _ => await ClearDemoDataAsync(), _ => CanRestore),
            OpenIntelligenceReviewCommand,
            _isIntelligenceReviewEnabled);
    }

    private void OpenIntelligenceReview()
    {
        if (!_isIntelligenceReviewEnabled)
        {
            _notificationService.ShowWarning(
                "Document Intelligence review dang tat trong cau hinh.",
                "Document Intelligence");
            return;
        }

        var window = _serviceProvider.GetRequiredService<DocumentExtractionReviewWindow>();
        window.Owner = global::System.Windows.Application.Current?.MainWindow;
        window.ShowDialog();
    }

    private void OpenServerSettings()
    {
        var window = _serviceProvider.GetRequiredService<ServerSettingsWindow>();
        window.Owner = global::System.Windows.Application.Current?.MainWindow;
        window.ShowDialog();

        if (CurrentView is SystemInfoViewModel systemInfo)
        {
            systemInfo.ApiBaseUrl = _apiService.BaseUrl;
        }
    }

    private async Task SeedDemoDataAsync()
    {
        try
        {
            IsSystemOperationRunning = true;
            var result = await _apiService.SeedDemoDataAsync();
            await RefreshCurrentViewAsync();
            _notificationService.ShowSuccess(
                $"Demo data sẵn sàng: {result.ActiveDemoCount:N0} văn bản.",
                "Demo data");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                $"Không thể tạo demo data: {ex.Message}",
                "Demo data");
        }
        finally
        {
            IsSystemOperationRunning = false;
        }
    }

    private async Task ClearDemoDataAsync()
    {
        var confirmed = _confirmDialogService.Confirm(
            "Chỉ các văn bản demo có mã DEMO-2026 và marker demo batch sẽ bị xóa mềm. Dữ liệu thật không bị ảnh hưởng. Tiếp tục?",
            "Clear Demo Data",
            "Clear Demo Data",
            "Hủy",
            ConfirmDialogType.Warning);

        if (!confirmed)
        {
            return;
        }

        try
        {
            IsSystemOperationRunning = true;
            var result = await _apiService.ClearDemoDataAsync();
            await RefreshCurrentViewAsync();
            _notificationService.ShowSuccess(
                $"Đã xóa mềm {result.AffectedCount:N0} văn bản demo. Còn lại: {result.ActiveDemoCount:N0}.",
                "Demo data");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                $"Không thể clear demo data: {ex.Message}",
                "Demo data");
        }
        finally
        {
            IsSystemOperationRunning = false;
        }
    }

    private void ShowPlaceholder(string title, string description)
    {
        CurrentView = new PlaceholderViewModel(title, description);
    }

    private void ShowUnavailableModule(string title)
    {
        ShowPlaceholder(
            title,
            "Chức năng này chưa sẵn sàng cho vận hành. Vui lòng sử dụng các luồng văn bản hiện có trong khi chờ triển khai chính thức.");
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
            return;
        }

        if (CurrentView is OperationalReportsViewModel)
        {
            await ShowReportsAsync();
            return;
        }

        if (CurrentView is LookupCatalogViewModel)
        {
            await ShowCategoriesAsync();
        }
    }
}

public class PlaceholderViewModel : BaseViewModel
{
    public PlaceholderViewModel(string title, string description)
    {
        Title = title;
        Description = description;
    }

    public string Title { get; }

    public string Description { get; }
}

public class OperationalReportsViewModel : BaseViewModel
{
    private int _totalDocuments;
    private int _issuedDocuments;
    private int _expiredDocuments;
    private int _effectiveDocuments;
    private string _topDepartment = "Chua co du lieu";
    private int _topDepartmentCount;

    public int TotalDocuments
    {
        get => _totalDocuments;
        private set => SetProperty(ref _totalDocuments, value);
    }

    public int IssuedDocuments
    {
        get => _issuedDocuments;
        private set => SetProperty(ref _issuedDocuments, value);
    }

    public int ExpiredDocuments
    {
        get => _expiredDocuments;
        private set => SetProperty(ref _expiredDocuments, value);
    }

    public int EffectiveDocuments
    {
        get => _effectiveDocuments;
        private set => SetProperty(ref _effectiveDocuments, value);
    }

    public string TopDepartment
    {
        get => _topDepartment;
        private set => SetProperty(ref _topDepartment, value);
    }

    public int TopDepartmentCount
    {
        get => _topDepartmentCount;
        private set => SetProperty(ref _topDepartmentCount, value);
    }

    public ObservableCollection<DashboardChartItemDto> EffectivenessChart { get; } = new();

    public ObservableCollection<DashboardChartItemDto> MonthlyIssuedChart { get; } = new();

    public ObservableCollection<DashboardChartItemDto> DepartmentIssuedChart { get; } = new();

    public void Apply(DashboardDto dashboard)
    {
        TotalDocuments = dashboard.Summary.TotalDocuments;
        IssuedDocuments = dashboard.Summary.IssuedDocuments;
        ExpiredDocuments = dashboard.Summary.ExpiredDocuments;
        EffectiveDocuments = dashboard.Summary.EffectiveDocuments;
        TopDepartment = dashboard.Summary.TopIssuingDepartment;
        TopDepartmentCount = dashboard.Summary.TopIssuingDepartmentCount;

        Replace(EffectivenessChart, dashboard.EffectivenessChart);
        Replace(MonthlyIssuedChart, dashboard.MonthlyIssuedChart);
        Replace(DepartmentIssuedChart, dashboard.DepartmentIssuedChart);
    }

    private static void Replace(ObservableCollection<DashboardChartItemDto> target, IEnumerable<DashboardChartItemDto> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }
}

public class LookupCatalogViewModel : BaseViewModel
{
    public ObservableCollection<LookupItemDto> Categories { get; } = new();

    public ObservableCollection<LookupItemDto> Statuses { get; } = new();

    public void Apply(IEnumerable<LookupItemDto> categories, IEnumerable<LookupItemDto> statuses)
    {
        Categories.Clear();
        foreach (var category in categories.OrderBy(x => x.Name))
        {
            Categories.Add(category);
        }

        Statuses.Clear();
        foreach (var status in statuses.OrderBy(x => x.Id))
        {
            Statuses.Add(status);
        }
    }
}

public class SystemInfoViewModel : BaseViewModel
{
    private string _apiBaseUrl;

    public SystemInfoViewModel(
        string displayName,
        string roleName,
        string departmentName,
        string apiBaseUrl,
        bool canBackup,
        bool canRestore,
        ICommand backupCommand,
        ICommand restoreCommand,
        ICommand serverSettingsCommand,
        ICommand seedDemoDataCommand,
        ICommand clearDemoDataCommand,
        ICommand intelligenceReviewCommand,
        bool isIntelligenceReviewEnabled)
    {
        DisplayName = displayName;
        RoleName = roleName;
        DepartmentName = departmentName;
        _apiBaseUrl = apiBaseUrl;
        CanBackup = canBackup;
        CanRestore = canRestore;
        BackupCommand = backupCommand;
        RestoreCommand = restoreCommand;
        ServerSettingsCommand = serverSettingsCommand;
        SeedDemoDataCommand = seedDemoDataCommand;
        ClearDemoDataCommand = clearDemoDataCommand;
        IntelligenceReviewCommand = intelligenceReviewCommand;
        IsIntelligenceReviewEnabled = isIntelligenceReviewEnabled;
    }

    public string DisplayName { get; }

    public string RoleName { get; }

    public string DepartmentName { get; }

    public string ApiBaseUrl
    {
        get => _apiBaseUrl;
        set => SetProperty(ref _apiBaseUrl, value);
    }

    public bool CanBackup { get; }

    public bool CanRestore { get; }

    public ICommand BackupCommand { get; }

    public ICommand RestoreCommand { get; }

    public ICommand ServerSettingsCommand { get; }

    public ICommand SeedDemoDataCommand { get; }

    public ICommand ClearDemoDataCommand { get; }

    public ICommand IntelligenceReviewCommand { get; }

    public bool IsIntelligenceReviewEnabled { get; }
}
