using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DocumentManagement.Contracts.Common;
using DocumentManagement.Contracts.Documents;
using DocumentManagement.Wpf.Commands;
using DocumentManagement.Wpf.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentManagement.Wpf.ViewModels;

public class ArchiveViewModel : BaseViewModel
{
    private readonly ApiService _apiService;
    private readonly IServiceProvider _serviceProvider;
    private readonly INotificationService _notificationService;
    private readonly ClientPermissionService _permissionService;

    private string? _searchText;
    private DocumentDto? _selectedDocument;
    private bool _isLoading;
    private int _pageNumber = 1;
    private int _pageSize = 100;
    private int _totalCount;
    private int _totalPages;
    private string _statusMessage = "Sẵn sàng";

    public ObservableCollection<DocumentDto> Documents { get; } = new();

    public string? SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                PageNumber = 1;
                _ = SearchAsync();
            }
        }
    }

    public DocumentDto? SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            if (SetProperty(ref _selectedDocument, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public int PageNumber
    {
        get => _pageNumber;
        set
        {
            if (SetProperty(ref _pageNumber, value))
            {
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(PageDisplayText));
            }
        }
    }

    public int TotalCount
    {
        get => _totalCount;
        set => SetProperty(ref _totalCount, value);
    }

    public int TotalPages
    {
        get => _totalPages;
        set
        {
            if (SetProperty(ref _totalPages, value))
            {
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(PageDisplayText));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool HasDocuments => Documents.Count > 0;

    public bool CanGoPrevious => PageNumber > 1;

    public bool CanGoNext => TotalPages > 0 && PageNumber < TotalPages;

    public string PageDisplayText => TotalPages <= 0 ? "Trang 0/0" : $"Trang {PageNumber}/{TotalPages}";

    public bool CanViewArchive => _permissionService.CanViewArchive();

    public bool CanRestoreArchive => _permissionService.CanEditDocuments();

    public ICommand RefreshCommand { get; }

    public ICommand PreviousPageCommand { get; }

    public ICommand NextPageCommand { get; }

    public ICommand OpenSelectedDocumentCommand { get; }

    public ICommand RestoreSelectedCommand { get; }

    public ArchiveViewModel(
        ApiService apiService,
        IServiceProvider serviceProvider,
        INotificationService notificationService,
        ClientPermissionService permissionService)
    {
        _apiService = apiService;
        _serviceProvider = serviceProvider;
        _notificationService = notificationService;
        _permissionService = permissionService;

        Documents.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasDocuments));

        RefreshCommand = new RelayCommand(async _ => await LoadAsync());
        PreviousPageCommand = new RelayCommand(async _ =>
        {
            if (!CanGoPrevious)
            {
                return;
            }

            PageNumber--;
            await SearchAsync();
        });
        NextPageCommand = new RelayCommand(async _ =>
        {
            if (!CanGoNext)
            {
                return;
            }

            PageNumber++;
            await SearchAsync();
        });
        OpenSelectedDocumentCommand = new RelayCommand(
            async _ => await OpenSelectedDocumentAsync(),
            _ => SelectedDocument != null && CanViewArchive);
        RestoreSelectedCommand = new RelayCommand(
            async _ => await RestoreSelectedAsync(),
            _ => SelectedDocument != null && CanRestoreArchive);
    }

    public async Task LoadAsync()
    {
        OnPropertyChanged(nameof(CanViewArchive));
        OnPropertyChanged(nameof(CanRestoreArchive));
        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        try
        {
            IsLoading = true;

            if (!CanViewArchive)
            {
                Documents.Clear();
                StatusMessage = "Bạn không có quyền xem lưu trữ.";
                return;
            }

            PagedResultDto<DocumentDto> result = await _apiService.SearchDocumentsAsync(
                SearchText,
                null,
                5,
                null,
                null,
                null,
                PageNumber,
                _pageSize);

            Documents.Clear();

            foreach (var document in result.Items)
            {
                Documents.Add(document);
            }

            TotalCount = result.TotalCount;
            TotalPages = result.TotalPages;
            StatusMessage = TotalCount == 0
                ? "Không có văn bản lưu trữ."
                : $"Trang {PageNumber}/{TotalPages} - {TotalCount} văn bản lưu trữ";
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi tải dữ liệu lưu trữ";
            _notificationService.ShowError("Không thể tải lưu trữ: " + ex.Message, "Lỗi");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasDocuments));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(PageDisplayText));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task OpenSelectedDocumentAsync()
    {
        if (SelectedDocument == null)
        {
            return;
        }

        var vm = _serviceProvider.GetRequiredService<DocumentFormViewModel>();
        await vm.LoadDocumentAsync(SelectedDocument.Id);
        vm.ApplyAccessMode(isReadOnly: !CanRestoreArchive);

        var window = new Views.DocumentFormWindow(vm)
        {
            Owner = Application.Current?.MainWindow
        };

        if (window.ShowDialog() == true)
        {
            await SearchAsync();
        }
    }

    private async Task RestoreSelectedAsync()
    {
        if (SelectedDocument == null)
        {
            return;
        }

        try
        {
            IsLoading = true;
            await _apiService.RestoreArchivedDocumentAsync(SelectedDocument.Id);
            await SearchAsync();
            _notificationService.ShowSuccess("Đã khôi phục văn bản khỏi lưu trữ.", "Lưu trữ");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Không thể khôi phục văn bản: " + ex.Message, "Lỗi");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
