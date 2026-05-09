using System.Collections.ObjectModel;
using System.Windows.Input;
using DocumentManagement.Contracts.Catalog;
using DocumentManagement.Wpf.Commands;
using DocumentManagement.Wpf.Services;

namespace DocumentManagement.Wpf.ViewModels;

public class CatalogViewModel : BaseViewModel
{
    private readonly ApiService _apiService;
    private readonly INotificationService _notificationService;
    private readonly ClientPermissionService _permissionService;

    private CatalogItemDto? _selectedCategory;
    private CatalogItemDto? _selectedStatus;
    private string _categoryName = string.Empty;
    private bool _categoryIsActive = true;
    private string _statusCode = string.Empty;
    private string _statusName = string.Empty;
    private bool _statusIsActive = true;
    private bool _isLoading;
    private string _statusMessage = "Sẵn sàng";

    public ObservableCollection<CatalogItemDto> Categories { get; } = new();

    public ObservableCollection<CatalogItemDto> Statuses { get; } = new();

    public CatalogItemDto? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                CategoryName = value?.Name ?? string.Empty;
                CategoryIsActive = value?.IsActive ?? true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public CatalogItemDto? SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            if (SetProperty(ref _selectedStatus, value))
            {
                StatusCode = value?.Code ?? string.Empty;
                StatusName = value?.Name ?? string.Empty;
                StatusIsActive = value?.IsActive ?? true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string CategoryName
    {
        get => _categoryName;
        set => SetProperty(ref _categoryName, value);
    }

    public bool CategoryIsActive
    {
        get => _categoryIsActive;
        set => SetProperty(ref _categoryIsActive, value);
    }

    public string StatusCode
    {
        get => _statusCode;
        set => SetProperty(ref _statusCode, value);
    }

    public string StatusName
    {
        get => _statusName;
        set => SetProperty(ref _statusName, value);
    }

    public bool StatusIsActive
    {
        get => _statusIsActive;
        set => SetProperty(ref _statusIsActive, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool CanManageCatalog => _permissionService.CanManageCatalog();

    public ICommand RefreshCommand { get; }

    public ICommand NewCategoryCommand { get; }

    public ICommand SaveCategoryCommand { get; }

    public ICommand DeleteCategoryCommand { get; }

    public ICommand NewStatusCommand { get; }

    public ICommand SaveStatusCommand { get; }

    public ICommand DeleteStatusCommand { get; }

    public CatalogViewModel(
        ApiService apiService,
        INotificationService notificationService,
        ClientPermissionService permissionService)
    {
        _apiService = apiService;
        _notificationService = notificationService;
        _permissionService = permissionService;

        RefreshCommand = new RelayCommand(async _ => await LoadAsync());
        NewCategoryCommand = new RelayCommand(_ => ClearCategory());
        SaveCategoryCommand = new RelayCommand(async _ => await SaveCategoryAsync(), _ => CanManageCatalog);
        DeleteCategoryCommand = new RelayCommand(
            async _ => await DeleteCategoryAsync(),
            _ => CanManageCatalog && SelectedCategory != null);
        NewStatusCommand = new RelayCommand(_ => ClearStatus());
        SaveStatusCommand = new RelayCommand(async _ => await SaveStatusAsync(), _ => CanManageCatalog);
        DeleteStatusCommand = new RelayCommand(
            async _ => await DeleteStatusAsync(),
            _ => CanManageCatalog && SelectedStatus != null);
    }

    public async Task LoadAsync()
    {
        try
        {
            IsLoading = true;

            Categories.Clear();
            foreach (var item in await _apiService.GetCatalogCategoriesAsync(includeInactive: true))
            {
                Categories.Add(item);
            }

            Statuses.Clear();
            foreach (var item in await _apiService.GetCatalogStatusesAsync(includeInactive: true))
            {
                Statuses.Add(item);
            }

            StatusMessage = "Danh mục đã được cập nhật.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi tải danh mục";
            _notificationService.ShowError("Không thể tải danh mục: " + ex.Message, "Lỗi");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(CanManageCatalog));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task SaveCategoryAsync()
    {
        if (!CanManageCatalog)
        {
            return;
        }

        try
        {
            var request = new SaveCatalogItemRequest
            {
                Name = CategoryName,
                IsActive = CategoryIsActive
            };

            if (SelectedCategory == null)
            {
                await _apiService.CreateCategoryAsync(request);
            }
            else
            {
                await _apiService.UpdateCategoryAsync(SelectedCategory.Id, request);
            }

            ClearCategory();
            await LoadAsync();
            _notificationService.ShowSuccess("Đã lưu danh mục văn bản.", "Danh mục");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Không thể lưu danh mục: " + ex.Message, "Lỗi");
        }
    }

    private async Task DeleteCategoryAsync()
    {
        if (SelectedCategory == null || !CanManageCatalog)
        {
            return;
        }

        try
        {
            await _apiService.DeleteCategoryAsync(SelectedCategory.Id);
            ClearCategory();
            await LoadAsync();
            _notificationService.ShowSuccess("Đã ngưng sử dụng danh mục văn bản.", "Danh mục");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Không thể xóa danh mục: " + ex.Message, "Lỗi");
        }
    }

    private async Task SaveStatusAsync()
    {
        if (!CanManageCatalog)
        {
            return;
        }

        try
        {
            var request = new SaveCatalogItemRequest
            {
                Code = StatusCode,
                Name = StatusName,
                IsActive = StatusIsActive
            };

            if (SelectedStatus == null)
            {
                await _apiService.CreateStatusAsync(request);
            }
            else
            {
                await _apiService.UpdateStatusAsync(SelectedStatus.Id, request);
            }

            ClearStatus();
            await LoadAsync();
            _notificationService.ShowSuccess("Đã lưu trạng thái văn bản.", "Danh mục");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Không thể lưu trạng thái: " + ex.Message, "Lỗi");
        }
    }

    private async Task DeleteStatusAsync()
    {
        if (SelectedStatus == null || !CanManageCatalog)
        {
            return;
        }

        try
        {
            await _apiService.DeleteStatusAsync(SelectedStatus.Id);
            ClearStatus();
            await LoadAsync();
            _notificationService.ShowSuccess("Đã ngưng sử dụng trạng thái.", "Danh mục");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Không thể xóa trạng thái: " + ex.Message, "Lỗi");
        }
    }

    private void ClearCategory()
    {
        SelectedCategory = null;
        CategoryName = string.Empty;
        CategoryIsActive = true;
    }

    private void ClearStatus()
    {
        SelectedStatus = null;
        StatusCode = string.Empty;
        StatusName = string.Empty;
        StatusIsActive = true;
    }
}
