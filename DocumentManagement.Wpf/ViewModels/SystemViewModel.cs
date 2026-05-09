using System.Collections.ObjectModel;
using System.Windows.Input;
using DocumentManagement.Contracts.System;
using DocumentManagement.Wpf.Commands;
using DocumentManagement.Wpf.Services;

namespace DocumentManagement.Wpf.ViewModels;

public class SystemViewModel : BaseViewModel
{
    private readonly ApiService _apiService;
    private readonly INotificationService _notificationService;
    private readonly ClientPermissionService _permissionService;

    private UserAdminDto? _selectedUser;
    private string? _searchText;
    private string _username = string.Empty;
    private string _fullName = string.Empty;
    private string _department = string.Empty;
    private string? _password;
    private long _roleId;
    private bool _isActive = true;
    private bool _isLoading;
    private string _statusMessage = "Sẵn sàng";

    public ObservableCollection<UserAdminDto> Users { get; } = new();

    public ObservableCollection<RoleDto> Roles { get; } = new();

    public UserAdminDto? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetProperty(ref _selectedUser, value))
            {
                Username = value?.Username ?? string.Empty;
                FullName = value?.FullName ?? string.Empty;
                Department = value?.Department ?? string.Empty;
                RoleId = value?.RoleId ?? Roles.FirstOrDefault()?.Id ?? 0;
                IsActive = value?.IsActive ?? true;
                Password = null;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string? SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string FullName
    {
        get => _fullName;
        set => SetProperty(ref _fullName, value);
    }

    public string Department
    {
        get => _department;
        set => SetProperty(ref _department, value);
    }

    public string? Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public long RoleId
    {
        get => _roleId;
        set => SetProperty(ref _roleId, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
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

    public bool CanManageSystem => _permissionService.CanManageSystem();

    public ICommand RefreshCommand { get; }

    public ICommand SearchCommand { get; }

    public ICommand NewUserCommand { get; }

    public ICommand SaveUserCommand { get; }

    public ICommand DeleteUserCommand { get; }

    public SystemViewModel(
        ApiService apiService,
        INotificationService notificationService,
        ClientPermissionService permissionService)
    {
        _apiService = apiService;
        _notificationService = notificationService;
        _permissionService = permissionService;

        RefreshCommand = new RelayCommand(async _ => await LoadAsync());
        SearchCommand = new RelayCommand(async _ => await LoadUsersAsync());
        NewUserCommand = new RelayCommand(_ => ClearForm());
        SaveUserCommand = new RelayCommand(async _ => await SaveUserAsync(), _ => CanManageSystem);
        DeleteUserCommand = new RelayCommand(
            async _ => await DeleteUserAsync(),
            _ => CanManageSystem && SelectedUser != null);
    }

    public async Task LoadAsync()
    {
        try
        {
            IsLoading = true;

            Roles.Clear();
            foreach (var role in await _apiService.GetRolesAsync())
            {
                Roles.Add(role);
            }

            if (RoleId <= 0)
            {
                RoleId = Roles.FirstOrDefault()?.Id ?? 0;
            }

            await LoadUsersAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi tải cấu hình hệ thống";
            _notificationService.ShowError("Không thể tải cấu hình hệ thống: " + ex.Message, "Lỗi");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(CanManageSystem));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task LoadUsersAsync()
    {
        if (!CanManageSystem)
        {
            Users.Clear();
            StatusMessage = "Bạn không có quyền quản trị hệ thống.";
            return;
        }

        var users = await _apiService.SearchUsersAsync(SearchText, includeInactive: true);
        Users.Clear();

        foreach (var user in users)
        {
            Users.Add(user);
        }

        StatusMessage = $"{Users.Count} người dùng";
    }

    private async Task SaveUserAsync()
    {
        if (!CanManageSystem)
        {
            return;
        }

        try
        {
            var request = new SaveUserRequest
            {
                Username = Username,
                FullName = FullName,
                Department = Department,
                Password = Password,
                RoleId = RoleId,
                IsActive = IsActive
            };

            if (SelectedUser == null)
            {
                await _apiService.CreateUserAsync(request);
            }
            else
            {
                await _apiService.UpdateUserAsync(SelectedUser.Id, request);
            }

            ClearForm();
            await LoadUsersAsync();
            _notificationService.ShowSuccess("Đã lưu người dùng.", "Hệ thống");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Không thể lưu người dùng: " + ex.Message, "Lỗi");
        }
    }

    private async Task DeleteUserAsync()
    {
        if (SelectedUser == null || !CanManageSystem)
        {
            return;
        }

        try
        {
            await _apiService.DeleteUserAsync(SelectedUser.Id);
            ClearForm();
            await LoadUsersAsync();
            _notificationService.ShowSuccess("Đã ngưng kích hoạt người dùng.", "Hệ thống");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Không thể xóa người dùng: " + ex.Message, "Lỗi");
        }
    }

    private void ClearForm()
    {
        SelectedUser = null;
        Username = string.Empty;
        FullName = string.Empty;
        Department = string.Empty;
        Password = null;
        RoleId = Roles.FirstOrDefault()?.Id ?? 0;
        IsActive = true;
    }
}
