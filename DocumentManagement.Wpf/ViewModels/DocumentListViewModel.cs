using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using DocumentManagement.Contracts.Common;
using DocumentManagement.Contracts.Documents;
using DocumentManagement.Wpf.Commands;
using DocumentManagement.Wpf.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using OfficeOpenXml;

namespace DocumentManagement.Wpf.ViewModels;

public class DocumentListViewModel : BaseViewModel
{
    private readonly ApiService _apiService;
    private readonly IServiceProvider _serviceProvider;
    private readonly INotificationService _notificationService;
    private readonly ClientPermissionService _permissionService;

    private CancellationTokenSource? _searchCts;

    private string? _searchText;
    private DocumentDto? _selectedDocument;
    private long? _selectedCategoryId;
    private long? _selectedStatusId;
    private string? _selectedUrgency;
    private DateTime? _fromDate;
    private DateTime? _toDate;

    private bool _isLoading;
    private string _statusMessage = "Sẵn sàng";

    private int _pageNumber = 1;
    private int _pageSize = 100;
    private int _totalCount;
    private int _totalPages;

    public ObservableCollection<DocumentDto> Documents { get; } = new();

    public ObservableCollection<LookupItemDto> Categories { get; } = new();

    public ObservableCollection<LookupItemDto> Statuses { get; } = new();

    public ObservableCollection<OptionItem> UrgencyOptions { get; } = new()
    {
        new() { Text = "Tất cả", Value = null },
        new() { Text = "Thường", Value = "NORMAL" },
        new() { Text = "Khẩn", Value = "URGENT" },
        new() { Text = "Hỏa tốc", Value = "VERY_URGENT" }
    };

    public string? SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                PageNumber = 1;
                _ = DebouncedSearchAsync();
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

    public long? SelectedCategoryId
    {
        get => _selectedCategoryId;
        set
        {
            if (SetProperty(ref _selectedCategoryId, value))
            {
                PageNumber = 1;
            }
        }
    }

    public long? SelectedStatusId
    {
        get => _selectedStatusId;
        set
        {
            if (SetProperty(ref _selectedStatusId, value))
            {
                PageNumber = 1;
            }
        }
    }

    public string? SelectedUrgency
    {
        get => _selectedUrgency;
        set
        {
            if (SetProperty(ref _selectedUrgency, value))
            {
                PageNumber = 1;
            }
        }
    }

    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
            {
                PageNumber = 1;
            }
        }
    }

    public DateTime? ToDate
    {
        get => _toDate;
        set
        {
            if (SetProperty(ref _toDate, value))
            {
                PageNumber = 1;
            }
        }
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

    public int PageSize
    {
        get => _pageSize;
        set => SetProperty(ref _pageSize, value);
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

    public bool HasDocuments => Documents.Count > 0;

    public bool CanGoPrevious => PageNumber > 1;

    public bool CanGoNext => TotalPages > 0 && PageNumber < TotalPages;

    public string PageDisplayText => TotalPages <= 0
        ? "Trang 0/0"
        : $"Trang {PageNumber}/{TotalPages}";

    public bool CanCreateDocument => _permissionService.CanCreateDocuments();

    public bool CanEditDocument => _permissionService.CanEditDocuments();

    public bool CanViewDocument => _permissionService.CanViewDocuments();

    public bool CanExportExcel => _permissionService.CanExportDocuments();

    public ICommand AddCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand ClearFiltersCommand { get; }

    public ICommand ExportExcelCommand { get; }

    public ICommand PreviousPageCommand { get; }

    public ICommand NextPageCommand { get; }

    public ICommand OpenSelectedDocumentCommand { get; }

    public DocumentListViewModel(
        ApiService apiService,
        IServiceProvider serviceProvider,
        INotificationService notificationService,
        ClientPermissionService permissionService)
    {
        _apiService = apiService;
        _serviceProvider = serviceProvider;
        _notificationService = notificationService;
        _permissionService = permissionService;

        Documents.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasDocuments));
        };

        AddCommand = new RelayCommand(
            _ => OpenCreateForm(),
            _ => CanCreateDocument);

        RefreshCommand = new RelayCommand(async _ =>
        {
            PageNumber = 1;
            await LoadLookupsAsync();
            await SearchAsync();

            _notificationService.ShowSuccess(
                "Danh sách văn bản đã được làm mới.",
                "Làm mới dữ liệu");
        });

        ClearFiltersCommand = new RelayCommand(async _ =>
        {
            _searchCts?.Cancel(); // Hủy bỏ mọi tác vụ tìm kiếm đang chờ (nếu có)

            // Cập nhật trực tiếp backing field để tránh trigger side-effect gọi API nhiều lần
            _searchText = null;
            _selectedCategoryId = 0;
            _selectedStatusId = 0;
            _selectedUrgency = null;
            _fromDate = null;
            _toDate = null;
            _pageNumber = 1;

            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(SelectedCategoryId));
            OnPropertyChanged(nameof(SelectedStatusId));
            OnPropertyChanged(nameof(SelectedUrgency));
            OnPropertyChanged(nameof(FromDate));
            OnPropertyChanged(nameof(ToDate));
            OnPropertyChanged(nameof(PageNumber));

            await SearchAsync();

            _notificationService.ShowInfo(
                "Đã xóa toàn bộ bộ lọc.",
                "Bộ lọc");
        });

        ExportExcelCommand = new RelayCommand(
            async _ => await ExportToExcelAsync(),
            _ => CanExportExcel);

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
            _ => SelectedDocument != null && CanViewDocument);

        SelectedCategoryId = 0;
        SelectedStatusId = 0;
        SelectedUrgency = null;
    }

    public async Task LoadAsync()
    {
        RefreshPermissions();
        await InitializeAsync();
    }

    private void RefreshPermissions()
    {
        OnPropertyChanged(nameof(CanCreateDocument));
        OnPropertyChanged(nameof(CanEditDocument));
        OnPropertyChanged(nameof(CanViewDocument));
        OnPropertyChanged(nameof(CanExportExcel));

        CommandManager.InvalidateRequerySuggested();
    }

    private async Task InitializeAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Đang khởi tạo dữ liệu...";

            await LoadLookupsAsync();
            await SearchAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi khởi tạo dữ liệu";

            _notificationService.ShowError(
                "Lỗi khởi tạo dữ liệu: " + ex.Message,
                "Lỗi dữ liệu");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadLookupsAsync()
    {
        try
        {
            await LoadCategoriesAsync();
            await LoadStatusesAsync();

            SelectedCategoryId ??= 0;
            SelectedStatusId ??= 0;
        }
        catch (Exception ex)
        {
            LoadFallbackLookups();

            _notificationService.ShowWarning(
                "Không thể tải danh mục từ API. Đã dùng danh mục mặc định. Chi tiết: " + ex.Message,
                "Danh mục");
        }
    }

    private async Task LoadCategoriesAsync()
    {
        var categories = await _apiService.GetCategoriesAsync();

        Categories.Clear();

        Categories.Add(new LookupItemDto
        {
            Id = 0,
            Code = string.Empty,
            Name = "Tất cả"
        });

        foreach (var category in categories.OrderBy(x => x.Name))
        {
            Categories.Add(category);
        }
    }

    private async Task LoadStatusesAsync()
    {
        var statuses = await _apiService.GetStatusesAsync();

        Statuses.Clear();

        Statuses.Add(new LookupItemDto
        {
            Id = 0,
            Code = string.Empty,
            Name = "Tất cả"
        });

        foreach (var status in statuses.OrderBy(x => x.Id))
        {
            Statuses.Add(status);
        }

        if (Statuses.Count == 1)
        {
            AddFallbackStatuses();
        }
    }

    private void LoadFallbackLookups()
    {
        Categories.Clear();
        Categories.Add(new LookupItemDto
        {
            Id = 0,
            Code = string.Empty,
            Name = "Tất cả"
        });

        Statuses.Clear();
        Statuses.Add(new LookupItemDto
        {
            Id = 0,
            Code = string.Empty,
            Name = "Tất cả"
        });

        AddFallbackStatuses();

        SelectedCategoryId = 0;
        SelectedStatusId = 0;
    }

    private void AddFallbackStatuses()
    {
        Statuses.Add(new LookupItemDto
        {
            Id = 1,
            Code = "DRAFT",
            Name = "Bản nháp"
        });

        Statuses.Add(new LookupItemDto
        {
            Id = 2,
            Code = "PENDING_APPROVAL",
            Name = "Chờ duyệt"
        });

        Statuses.Add(new LookupItemDto
        {
            Id = 3,
            Code = "APPROVED",
            Name = "Đang xử lý"
        });

        Statuses.Add(new LookupItemDto
        {
            Id = 4,
            Code = "ISSUED",
            Name = "Đã ban hành"
        });

        Statuses.Add(new LookupItemDto
        {
            Id = 5,
            Code = "ARCHIVED",
            Name = "Đã lưu trữ"
        });

        Statuses.Add(new LookupItemDto
        {
            Id = 6,
            Code = "REJECTED",
            Name = "Bị từ chối"
        });
    }

    private async Task DebouncedSearchAsync()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(350, _searchCts.Token);
            await SearchAsync();
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi tìm kiếm";

            _notificationService.ShowError(
                "Lỗi tìm kiếm: " + ex.Message,
                "Lỗi");
        }
    }

    public async Task SearchAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Đang tải dữ liệu từ API...";

            if (!CanViewDocument)
            {
                Documents.Clear();
                TotalCount = 0;
                TotalPages = 0;
                StatusMessage = "Bạn không có quyền xem danh sách văn bản.";
                return;
            }

            if (FromDate.HasValue && ToDate.HasValue && FromDate > ToDate)
            {
                StatusMessage = "Khoảng ngày không hợp lệ";

                _notificationService.ShowWarning(
                    "Ngày bắt đầu không được lớn hơn ngày kết thúc.",
                    "Khoảng ngày không hợp lệ");

                return;
            }

            var result = await _apiService.SearchDocumentsAsync(
                SearchText,
                SelectedCategoryId,
                SelectedStatusId,
                SelectedUrgency,
                FromDate,
                ToDate,
                PageNumber,
                PageSize);

            Documents.Clear();

            foreach (var item in result.Items)
            {
                Documents.Add(item);
            }

            TotalCount = result.TotalCount;
            TotalPages = result.TotalPages;

            if (TotalPages > 0 && PageNumber > TotalPages)
            {
                PageNumber = TotalPages;
                await SearchAsync();
                return;
            }

            StatusMessage = TotalCount == 0
                ? "Không tìm thấy dữ liệu"
                : $"Trang {PageNumber}/{TotalPages} • {TotalCount} văn bản";
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi tải dữ liệu từ API";

            _notificationService.ShowError(
                "Lỗi gọi API: " + ex.Message,
                "Lỗi API");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasDocuments));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(PageDisplayText));
            RefreshPermissions();
        }
    }

    public async Task OpenSelectedDocumentAsync()
    {
        if (SelectedDocument == null)
        {
            _notificationService.ShowWarning(
                "Vui lòng chọn một văn bản để mở.",
                "Chưa chọn văn bản");
            return;
        }

        if (!CanViewDocument)
        {
            _notificationService.ShowWarning(
                "Bạn không có quyền xem văn bản.",
                "Từ chối truy cập");
            return;
        }

        try
        {
            var vm = _serviceProvider.GetRequiredService<DocumentFormViewModel>();
            await vm.LoadDocumentAsync(SelectedDocument.Id);

            vm.ApplyAccessMode(isReadOnly: !CanEditDocument);

            var win = new Views.DocumentFormWindow(vm)
            {
                Owner = Application.Current?.MainWindow
            };

            var result = win.ShowDialog();

            if (result == true)
            {
                await SearchAsync();

                _notificationService.ShowSuccess(
                    "Danh sách đã được cập nhật.",
                    "Cập nhật dữ liệu");
            }
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                "Không thể mở văn bản: " + ex.Message,
                "Lỗi");
        }
    }

    private void OpenCreateForm()
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

            var win = new Views.DocumentFormWindow(vm)
            {
                Owner = Application.Current?.MainWindow
            };

            if (win.ShowDialog() == true)
            {
                _ = SearchAsync();

                _notificationService.ShowSuccess(
                    "Văn bản mới đã được tạo.",
                    "Tạo văn bản");
            }
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                "Không thể mở form tạo văn bản: " + ex.Message,
                "Lỗi");
        }
    }

    private async Task ExportToExcelAsync()
    {
        if (!CanExportExcel)
        {
            _notificationService.ShowWarning(
                "Bạn không có quyền xuất Excel.",
                "Từ chối thao tác");
            return;
        }

        if (Documents.Count == 0)
        {
            _notificationService.ShowWarning(
                "Không có dữ liệu để xuất Excel.",
                "Không có dữ liệu");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"Bao_cao_{DateTime.Now:ddMMyyyy}.xlsx"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Đang xuất Excel...";

            await Task.Run(() =>
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                using var package = new ExcelPackage();
                var sheet = package.Workbook.Worksheets.Add("Documents");

                sheet.Cells[1, 1].Value = "STT";
                sheet.Cells[1, 2].Value = "Số hiệu";
                sheet.Cells[1, 3].Value = "Trích yếu";
                sheet.Cells[1, 4].Value = "Trạng thái";
                sheet.Cells[1, 5].Value = "Độ khẩn";
                sheet.Cells[1, 6].Value = "Ngày ban hành";

                var row = 2;

                foreach (var doc in Documents)
                {
                    sheet.Cells[row, 1].Value = row - 1 + ((PageNumber - 1) * PageSize);
                    sheet.Cells[row, 2].Value = doc.DocumentNumber;
                    sheet.Cells[row, 3].Value = doc.Title;
                    sheet.Cells[row, 4].Value = doc.StatusText;
                    sheet.Cells[row, 5].Value = doc.UrgencyText;
                    sheet.Cells[row, 6].Value = doc.IssueDate;
                    row++;
                }

                sheet.Cells.AutoFitColumns();

                File.WriteAllBytes(dialog.FileName, package.GetAsByteArray());
            });

            StatusMessage = "Xuất Excel thành công";

            _notificationService.ShowSuccess(
                "File Excel đã được xuất thành công.",
                "Xuất Excel");
        }
        catch (Exception ex)
        {
            StatusMessage = "Xuất Excel thất bại";

            _notificationService.ShowError(
                "Lỗi xuất Excel: " + ex.Message,
                "Lỗi");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
