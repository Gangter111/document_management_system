using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DocumentManagement.Contracts.Common;
using DocumentManagement.Contracts.Documents;
using DocumentManagement.Wpf.Commands;
using DocumentManagement.Wpf.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentManagement.Wpf.ViewModels;

public class DocumentListViewModel : BaseViewModel, IDisposable
{
    private const int MaxClientExportRows = 10000;
    private readonly DocumentSearchCoordinator _searchCoordinator;
    private readonly DocumentWorkflowCommandService _workflowService;
    private readonly DocumentExportService _exportService;
    private readonly ApiService _apiService;
    private readonly IServiceProvider _serviceProvider;
    private readonly INotificationService _notificationService;
    private readonly ClientPermissionService _permissionService;
    private readonly OperationTracker _operationTracker = new();
    private readonly Dictionary<long, DocumentRowViewModel> _batchSelectedRows = new();
    private readonly EventHandler _busyChangedHandler;

    private bool _suppressFilterReload;
    private bool _isActive = true;
    private bool _isDisposed;
    private CancellationTokenSource _lifetimeCts = new();
    private CancellationTokenSource? _exportCts;
    private long _searchRequestVersion;
    private string? _searchText;
    private DocumentRowViewModel? _selectedDocument;
    private DocumentQueueViewModel? _selectedQueue;
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

    public DocumentListViewModel(
        DocumentSearchCoordinator searchCoordinator,
        DocumentWorkflowCommandService workflowService,
        DocumentExportService exportService,
        ApiService apiService,
        IServiceProvider serviceProvider,
        INotificationService notificationService,
        ClientPermissionService permissionService)
    {
        _searchCoordinator = searchCoordinator;
        _workflowService = workflowService;
        _exportService = exportService;
        _apiService = apiService;
        _serviceProvider = serviceProvider;
        _notificationService = notificationService;
        _permissionService = permissionService;

        _busyChangedHandler = (_, _) =>
        {
            if (CanUpdateUi)
            {
                IsLoading = _operationTracker.IsBusy;
            }
        };
        _operationTracker.IsBusyChanged += _busyChangedHandler;

        BuildQueues();
        BuildCommands();

        Documents.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasDocuments));
            OnPropertyChanged(nameof(CurrentPageCount));
            OnPropertyChanged(nameof(GridStatusText));
        };

        _suppressFilterReload = true;
        SelectedCategoryId = 0;
        SelectedStatusId = 0;
        SelectedUrgency = null;
        SelectedQueue = Queues.FirstOrDefault();
        _suppressFilterReload = false;
    }

    private bool CanUpdateUi => _isActive && !_isDisposed;

    public ObservableCollection<DocumentRowViewModel> Documents { get; } = new();

    public ObservableCollection<DocumentQueueViewModel> Queues { get; } = new();

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
                QueueSearch(debounce: true);
            }
        }
    }

    public DocumentRowViewModel? SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            if (SetProperty(ref _selectedDocument, value))
            {
                OnPropertyChanged(nameof(HasSelectedDocument));
                OnPropertyChanged(nameof(PreviewTitle));
                OnPropertyChanged(nameof(PreviewNextAction));
                RaiseCommandState();
            }
        }
    }

    public DocumentQueueViewModel? SelectedQueue
    {
        get => _selectedQueue;
        set
        {
            if (SetProperty(ref _selectedQueue, value) && value != null)
            {
                ApplyQueue(value);
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
                QueueSearch();
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
                QueueSearch();
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
                QueueSearch();
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
                QueueSearch();
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
                QueueSearch();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RaiseCommandState();
            }
        }
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
        set
        {
            if (SetProperty(ref _totalCount, value))
            {
                OnPropertyChanged(nameof(GridStatusText));
            }
        }
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

    public bool HasSelectedDocument => SelectedDocument != null;

    public bool HasBatchSelection => BatchSelectedCount > 0;

    public int BatchSelectedCount => _batchSelectedRows.Count;

    public int CurrentPageCount => Documents.Count;

    public bool CanGoPrevious => PageNumber > 1;

    public bool CanGoNext => TotalPages > 0 && PageNumber < TotalPages;

    public string PageDisplayText => TotalPages <= 0
        ? "Trang 0/0"
        : $"Trang {PageNumber}/{TotalPages}";

    public string GridStatusText => TotalCount == 0
        ? "Không có văn bản phù hợp"
        : $"{CurrentPageCount:N0}/{TotalCount:N0} văn bản trong bộ lọc hiện tại";

    public string PreviewTitle => SelectedDocument?.Title ?? "Chưa chọn văn bản";

    public string PreviewNextAction
    {
        get
        {
            if (SelectedDocument == null)
            {
                return "Chọn một dòng để xem chi tiết và thao tác.";
            }

            if (SelectedDocument.IsArchived)
            {
                return CanRestoreSelected
                    ? "Văn bản đang lưu trữ. Có thể khôi phục nếu cần xử lý lại."
                    : "Văn bản đang lưu trữ.";
            }

            if (SelectedDocument.IsBlocked)
            {
                return "Đang quá hạn. Ưu tiên rà soát và cập nhật hạn xử lý.";
            }

            return CanArchiveSelected
                ? "Sẵn sàng xử lý. Mở để chỉnh sửa hoặc lưu trữ khi hoàn tất."
                : "Bạn có quyền xem văn bản này.";
        }
    }

    public bool CanCreateDocument => _permissionService.CanCreateDocuments();

    public bool CanEditDocument => _permissionService.CanEditDocuments();

    public bool CanViewDocument => _permissionService.CanViewDocuments();

    public bool CanExportExcel => _permissionService.CanExportDocuments();

    public bool CanOpenSelected => CanViewDocument && !IsLoading && SelectedDocument != null;

    public bool CanEditSelected => CanEditDocument && !IsLoading && SelectedDocument is { IsArchived: false };

    public bool CanArchiveSelected => CanEditDocument && !IsLoading && SelectedDocument is { IsArchived: false };

    public bool CanRestoreSelected => CanEditDocument && !IsLoading && SelectedDocument is { IsArchived: true };

    public bool CanArchiveBatch => CanEditDocument && !IsLoading && _batchSelectedRows.Count > 0 && _batchSelectedRows.Values.All(x => !x.IsArchived);

    public bool CanRestoreBatch => CanEditDocument && !IsLoading && _batchSelectedRows.Count > 0 && _batchSelectedRows.Values.All(x => x.IsArchived);

    public ICommand AddCommand => AddRelayCommand;

    public ICommand RefreshCommand => RefreshRelayCommand;

    public ICommand ClearFiltersCommand => ClearFiltersRelayCommand;

    public ICommand ExportExcelCommand => ExportExcelRelayCommand;

    public ICommand PreviousPageCommand => PreviousPageRelayCommand;

    public ICommand NextPageCommand => NextPageRelayCommand;

    public ICommand OpenSelectedDocumentCommand => OpenSelectedDocumentRelayCommand;

    public ICommand EditSelectedDocumentCommand => EditSelectedDocumentRelayCommand;

    public ICommand ArchiveSelectedCommand => ArchiveSelectedRelayCommand;

    public ICommand RestoreSelectedCommand => RestoreSelectedRelayCommand;

    public ICommand ArchiveBatchCommand => ArchiveBatchRelayCommand;

    public ICommand RestoreBatchCommand => RestoreBatchRelayCommand;

    public ICommand SelectAllBatchCommand => SelectAllBatchRelayCommand;

    public ICommand ClearBatchCommand => ClearBatchRelayCommand;

    private RelayCommand AddRelayCommand { get; set; } = null!;

    private RelayCommand RefreshRelayCommand { get; set; } = null!;

    private RelayCommand ClearFiltersRelayCommand { get; set; } = null!;

    private RelayCommand ExportExcelRelayCommand { get; set; } = null!;

    private RelayCommand PreviousPageRelayCommand { get; set; } = null!;

    private RelayCommand NextPageRelayCommand { get; set; } = null!;

    private RelayCommand OpenSelectedDocumentRelayCommand { get; set; } = null!;

    private RelayCommand EditSelectedDocumentRelayCommand { get; set; } = null!;

    private RelayCommand ArchiveSelectedRelayCommand { get; set; } = null!;

    private RelayCommand RestoreSelectedRelayCommand { get; set; } = null!;

    private RelayCommand ArchiveBatchRelayCommand { get; set; } = null!;

    private RelayCommand RestoreBatchRelayCommand { get; set; } = null!;

    private RelayCommand SelectAllBatchRelayCommand { get; set; } = null!;

    private RelayCommand ClearBatchRelayCommand { get; set; } = null!;

    public async Task LoadAsync()
    {
        if (!CanUpdateUi)
        {
            return;
        }

        RefreshPermissions();
        await InitializeAsync();
    }

    public void Deactivate()
    {
        if (_isDisposed)
        {
            return;
        }

        _isActive = false;
        CancelActiveOperations();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _isActive = false;
        CancelActiveOperations();

        _operationTracker.IsBusyChanged -= _busyChangedHandler;
        foreach (var row in Documents)
        {
            row.BatchSelectionChanged -= Row_BatchSelectionChanged;
        }

        _batchSelectedRows.Clear();
        _searchCoordinator.Dispose();
        _lifetimeCts.Dispose();
    }

    private void CancelActiveOperations()
    {
        _searchCoordinator.Cancel();
        _exportCts?.Cancel();

        if (!_lifetimeCts.IsCancellationRequested)
        {
            _lifetimeCts.Cancel();
        }
    }

    public async Task OpenSelectedDocumentAsync(bool forceEdit = false)
    {
        if (!CanUpdateUi || SelectedDocument == null)
        {
            if (CanUpdateUi)
            {
                _notificationService.ShowWarning("Vui lòng chọn một văn bản để mở.", "Chưa chọn văn bản");
            }
            return;
        }

        if (!CanViewDocument)
        {
            _notificationService.ShowWarning("Bạn không có quyền xem văn bản.", "Từ chối truy cập");
            return;
        }

        if (forceEdit && SelectedDocument.IsArchived)
        {
            _notificationService.ShowWarning(
                "Văn bản đã lưu trữ không thể chỉnh sửa. Hãy khôi phục trước khi cập nhật.",
                "Không thể chỉnh sửa");
            return;
        }

        try
        {
            using var _ = _operationTracker.Begin();

            var vm = _serviceProvider.GetRequiredService<DocumentFormViewModel>();
            await vm.LoadDocumentAsync(SelectedDocument.Id);
            if (!CanUpdateUi)
            {
                return;
            }

            vm.ApplyAccessMode(isReadOnly: !forceEdit || !CanEditDocument);

            var win = new Views.DocumentFormWindow(vm)
            {
                Owner = Application.Current?.MainWindow
            };

            if (win.ShowDialog() == true)
            {
                if (!CanUpdateUi)
                {
                    return;
                }

                await SearchAsync();
                if (CanUpdateUi)
                {
                    _notificationService.ShowSuccess("Danh sách đã được cập nhật.", "Cập nhật dữ liệu");
                }
            }
        }
        catch (Exception ex)
        {
            if (CanUpdateUi)
            {
                _notificationService.ShowError("Không thể mở văn bản: " + ex.Message, "Lỗi");
            }
        }
    }

    private void BuildCommands()
    {
        AddRelayCommand = new RelayCommand(_ => OpenCreateForm(), _ => CanCreateDocument && !IsLoading);
        RefreshRelayCommand = new RelayCommand(async _ => await RefreshAsync(), _ => !IsLoading);
        ClearFiltersRelayCommand = new RelayCommand(async _ => await ClearFiltersAsync(), _ => !IsLoading);
        ExportExcelRelayCommand = new RelayCommand(async _ => await ExportToExcelAsync(), _ => CanExportExcel && !IsLoading && TotalCount > 0);
        PreviousPageRelayCommand = new RelayCommand(async _ => await GoToPreviousPageAsync(), _ => CanGoPrevious && !IsLoading);
        NextPageRelayCommand = new RelayCommand(async _ => await GoToNextPageAsync(), _ => CanGoNext && !IsLoading);
        OpenSelectedDocumentRelayCommand = new RelayCommand(async parameter =>
        {
            SelectRowParameter(parameter);
            await OpenSelectedDocumentAsync();
        }, parameter => CanViewDocument && !IsLoading && (parameter is DocumentRowViewModel || SelectedDocument != null));
        EditSelectedDocumentRelayCommand = new RelayCommand(async parameter =>
        {
            SelectRowParameter(parameter);
            await OpenSelectedDocumentAsync(forceEdit: true);
        }, parameter => CanEditDocument && !IsLoading && GetCommandRow(parameter) is { IsArchived: false });
        ArchiveSelectedRelayCommand = new RelayCommand(async parameter =>
        {
            SelectRowParameter(parameter);
            await ArchiveSelectedAsync();
        }, parameter => CanEditDocument && !IsLoading && GetCommandRow(parameter) is { IsArchived: false });
        RestoreSelectedRelayCommand = new RelayCommand(async parameter =>
        {
            SelectRowParameter(parameter);
            await RestoreSelectedAsync();
        }, parameter => CanEditDocument && !IsLoading && GetCommandRow(parameter) is { IsArchived: true });
        ArchiveBatchRelayCommand = new RelayCommand(async _ => await ChangeBatchStatusAsync(archive: true), _ => CanArchiveBatch);
        RestoreBatchRelayCommand = new RelayCommand(async _ => await ChangeBatchStatusAsync(archive: false), _ => CanRestoreBatch);
        SelectAllBatchRelayCommand = new RelayCommand(_ => SetAllBatchSelection(true), _ => Documents.Count > 0 && !IsLoading);
        ClearBatchRelayCommand = new RelayCommand(_ => SetAllBatchSelection(false), _ => HasBatchSelection && !IsLoading);
    }

    private void BuildQueues()
    {
        Queues.Clear();
        Queues.Add(new DocumentQueueViewModel("ALL", "Tất cả", "Toàn bộ văn bản đang hoạt động"));
        Queues.Add(new DocumentQueueViewModel("DRAFT", "Bản nháp", "Văn bản cần hoàn thiện", DocumentWorkflowStatus.Draft));
        Queues.Add(new DocumentQueueViewModel("ISSUED", "Đã ban hành", "Văn bản đã phát hành", DocumentWorkflowStatus.Issued));
        Queues.Add(new DocumentQueueViewModel("URGENT", "Khẩn", "Văn bản khẩn và hỏa tốc", null, "URGENT"));
        Queues.Add(new DocumentQueueViewModel("ARCHIVED", "Lưu trữ", "Văn bản đã đưa vào kho lưu trữ", DocumentWorkflowStatus.Archived));
    }

    private void ApplyQueue(DocumentQueueViewModel queue)
    {
        if (_suppressFilterReload)
        {
            return;
        }

        _suppressFilterReload = true;
        SelectedStatusId = queue.StatusId ?? 0;
        SelectedUrgency = queue.Urgency;
        PageNumber = 1;
        _suppressFilterReload = false;

        QueueSearch();
    }

    private async Task InitializeAsync()
    {
        try
        {
            using var _ = _operationTracker.Begin();
            if (!CanUpdateUi)
            {
                return;
            }

            StatusMessage = "Đang khởi tạo dữ liệu...";

            await LoadLookupsAsync();
            await SearchAsync();
        }
        catch (Exception ex)
        {
            if (!CanUpdateUi)
            {
                return;
            }

            StatusMessage = "Lỗi khởi tạo dữ liệu";
            _notificationService.ShowError("Lỗi khởi tạo dữ liệu: " + ex.Message, "Lỗi dữ liệu");
        }
    }

    private async Task RefreshAsync()
    {
        if (!CanUpdateUi)
        {
            return;
        }

        PageNumber = 1;
        await LoadLookupsAsync();
        await SearchAsync();

        if (CanUpdateUi)
        {
            _notificationService.ShowSuccess("Danh sách văn bản đã được làm mới.", "Làm mới dữ liệu");
        }
    }

    private async Task ClearFiltersAsync()
    {
        if (!CanUpdateUi)
        {
            return;
        }

        _searchCoordinator.Cancel();

        _suppressFilterReload = true;
        SearchText = null;
        SelectedCategoryId = 0;
        SelectedStatusId = 0;
        SelectedUrgency = null;
        FromDate = null;
        ToDate = null;
        PageNumber = 1;
        SelectedQueue = Queues.FirstOrDefault();
        _suppressFilterReload = false;

        await SearchAsync();
        if (CanUpdateUi)
        {
            _notificationService.ShowInfo("Đã xóa toàn bộ bộ lọc.", "Bộ lọc");
        }
    }

    private async Task GoToPreviousPageAsync()
    {
        if (!CanGoPrevious)
        {
            return;
        }

        PageNumber--;
        await SearchAsync();
    }

    private async Task GoToNextPageAsync()
    {
        if (!CanGoNext)
        {
            return;
        }

        PageNumber++;
        await SearchAsync();
    }

    private void QueueSearch(bool debounce = false)
    {
        if (_suppressFilterReload || !CanUpdateUi)
        {
            return;
        }

        _ = SearchAsync(debounce ? TimeSpan.FromMilliseconds(350) : TimeSpan.Zero);
    }

    private async Task SearchAsync(TimeSpan? debounce = null)
    {
        if (!CanUpdateUi)
        {
            return;
        }

        var requestVersion = Interlocked.Increment(ref _searchRequestVersion);
        if (debounce.HasValue && debounce.Value > TimeSpan.Zero)
        {
            try
            {
                _searchCoordinator.Cancel();
                await Task.Delay(debounce.Value, _lifetimeCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!CanUpdateUi || requestVersion != Volatile.Read(ref _searchRequestVersion))
            {
                return;
            }
        }

        var activeDocumentId = SelectedDocument?.Id;
        var canFinalizeUi = false;

        try
        {
            using var _ = _operationTracker.Begin();
            if (!CanUpdateUi)
            {
                return;
            }

            StatusMessage = "Đang tải dữ liệu từ API...";

            if (!CanViewDocument)
            {
                ReplaceRows([]);
                TotalCount = 0;
                TotalPages = 0;
                StatusMessage = "Bạn không có quyền xem danh sách văn bản.";
                canFinalizeUi = true;
                return;
            }

            if (FromDate.HasValue && ToDate.HasValue && FromDate > ToDate)
            {
                StatusMessage = "Khoảng ngày không hợp lệ";
                _notificationService.ShowWarning("Ngày bắt đầu không được lớn hơn ngày kết thúc.", "Khoảng ngày không hợp lệ");
                return;
            }

            var result = await _searchCoordinator.SearchAsync(CreateSearchCriteria(), TimeSpan.Zero);
            if (!CanUpdateUi || result.IsStale || result.Result == null)
            {
                return;
            }

            ReplaceRows(result.Result.Items);
            canFinalizeUi = true;

            TotalCount = result.Result.TotalCount;
            TotalPages = result.Result.TotalPages;

            if (TotalPages > 0 && PageNumber > TotalPages)
            {
                PageNumber = TotalPages;
                await SearchAsync();
                return;
            }

            RestoreActiveRow(activeDocumentId);

            StatusMessage = TotalCount == 0
                ? "Không tìm thấy dữ liệu"
                : $"Trang {PageNumber}/{TotalPages} - {TotalCount:N0} văn bản";
        }
        catch (OperationCanceledException) when (!CanUpdateUi)
        {
        }
        catch (Exception ex)
        {
            if (!CanUpdateUi)
            {
                return;
            }

            canFinalizeUi = true;
            StatusMessage = "Lỗi tải dữ liệu từ API";
            _notificationService.ShowError("Lỗi gọi API: " + ex.Message, "Lỗi API");
        }
        finally
        {
            if (CanUpdateUi && canFinalizeUi)
            {
                RefreshDerivedState();
                RefreshPermissions();
            }
        }
    }

    private DocumentSearchCriteria CreateSearchCriteria()
    {
        return new DocumentSearchCriteria(
            SearchText,
            SelectedCategoryId,
            SelectedStatusId,
            SelectedUrgency,
            FromDate,
            ToDate,
            PageNumber,
            PageSize);
    }

    private async Task LoadLookupsAsync()
    {
        if (!CanUpdateUi)
        {
            return;
        }

        try
        {
            await LoadCategoriesAsync();
            if (!CanUpdateUi)
            {
                return;
            }

            await LoadStatusesAsync();

            SelectedCategoryId ??= 0;
            SelectedStatusId ??= 0;
        }
        catch (Exception ex)
        {
            if (!CanUpdateUi)
            {
                return;
            }

            LoadFallbackLookups();
            _notificationService.ShowWarning(
                "Không thể tải danh mục từ API. Đã dùng danh mục mặc định. Chi tiết: " + ex.Message,
                "Danh mục");
        }
    }

    private async Task LoadCategoriesAsync()
    {
        var categories = await _apiService.GetCategoriesAsync();
        if (!CanUpdateUi)
        {
            return;
        }

        Categories.Clear();
        Categories.Add(new LookupItemDto { Id = 0, Code = string.Empty, Name = "Tất cả" });

        foreach (var category in categories.OrderBy(x => x.Name))
        {
            Categories.Add(category);
        }
    }

    private async Task LoadStatusesAsync()
    {
        var statuses = await _apiService.GetStatusesAsync();
        if (!CanUpdateUi)
        {
            return;
        }

        Statuses.Clear();
        Statuses.Add(new LookupItemDto { Id = 0, Code = string.Empty, Name = "Tất cả" });

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
        Categories.Add(new LookupItemDto { Id = 0, Code = string.Empty, Name = "Tất cả" });

        Statuses.Clear();
        Statuses.Add(new LookupItemDto { Id = 0, Code = string.Empty, Name = "Tất cả" });
        AddFallbackStatuses();

        _selectedCategoryId = 0;
        _selectedStatusId = 0;
        OnPropertyChanged(nameof(SelectedCategoryId));
        OnPropertyChanged(nameof(SelectedStatusId));
    }

    private void AddFallbackStatuses()
    {
        Statuses.Add(new LookupItemDto { Id = DocumentWorkflowStatus.Draft, Code = "DRAFT", Name = "Bản nháp" });
        Statuses.Add(new LookupItemDto { Id = DocumentWorkflowStatus.Issued, Code = "ISSUED", Name = "Đã ban hành" });
        Statuses.Add(new LookupItemDto { Id = DocumentWorkflowStatus.Archived, Code = "ARCHIVED", Name = "Đã lưu trữ" });
    }

    private void ReplaceRows(IEnumerable<DocumentDto> items)
    {
        foreach (var row in Documents)
        {
            row.BatchSelectionChanged -= Row_BatchSelectionChanged;
        }

        Documents.Clear();
        _batchSelectedRows.Clear();

        foreach (var item in items)
        {
            var row = new DocumentRowViewModel(item);
            row.BatchSelectionChanged += Row_BatchSelectionChanged;
            Documents.Add(row);
        }

        RefreshBatchState();
    }

    private void Row_BatchSelectionChanged(object? sender, bool isSelected)
    {
        if (sender is not DocumentRowViewModel row)
        {
            return;
        }

        if (isSelected)
        {
            _batchSelectedRows[row.Id] = row;
        }
        else
        {
            _batchSelectedRows.Remove(row.Id);
        }

        RefreshBatchState();
    }

    private void RestoreActiveRow(long? activeDocumentId)
    {
        if (activeDocumentId.HasValue)
        {
            var previous = Documents.FirstOrDefault(x => x.Id == activeDocumentId.Value);
            if (previous != null)
            {
                SelectedDocument = previous;
                return;
            }
        }

        SelectedDocument = null;
    }

    private async Task ArchiveSelectedAsync()
    {
        if (SelectedDocument == null)
        {
            return;
        }

        await ChangeStatusAsync(SelectedDocument, archive: true);
    }

    private async Task RestoreSelectedAsync()
    {
        if (SelectedDocument == null)
        {
            return;
        }

        await ChangeStatusAsync(SelectedDocument, archive: false);
    }

    private async Task ChangeBatchStatusAsync(bool archive)
    {
        if (!CanUpdateUi)
        {
            return;
        }

        var rows = _batchSelectedRows.Values.ToList();
        if (rows.Count == 0)
        {
            return;
        }

        var operationName = archive ? "lưu trữ" : "khôi phục";

        try
        {
            using var _ = _operationTracker.Begin();
            StatusMessage = $"Đang {operationName} {rows.Count:N0} văn bản...";

            foreach (var row in rows)
            {
                _lifetimeCts.Token.ThrowIfCancellationRequested();
                if (!CanUpdateUi)
                {
                    return;
                }

                if (archive)
                {
                    await _workflowService.ArchiveAsync(row, _lifetimeCts.Token);
                }
                else
                {
                    await _workflowService.RestoreAsync(row, _lifetimeCts.Token);
                }
            }

            _lifetimeCts.Token.ThrowIfCancellationRequested();
            await SearchAsync();
            if (CanUpdateUi)
            {
                _notificationService.ShowSuccess($"Đã {operationName} {rows.Count:N0} văn bản.", "Cập nhật trạng thái");
            }
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                return;
            }

            if (CanUpdateUi)
            {
                _notificationService.ShowError($"Không thể {operationName} hàng loạt: {ex.Message}", "Lỗi workflow");
            }
        }
    }

    private async Task ChangeStatusAsync(DocumentRowViewModel row, bool archive)
    {
        if (!CanUpdateUi)
        {
            return;
        }

        try
        {
            using var _ = _operationTracker.Begin();
            StatusMessage = "Đang cập nhật trạng thái văn bản...";

            _lifetimeCts.Token.ThrowIfCancellationRequested();
            if (archive)
            {
                await _workflowService.ArchiveAsync(row, _lifetimeCts.Token);
            }
            else
            {
                await _workflowService.RestoreAsync(row, _lifetimeCts.Token);
            }

            _lifetimeCts.Token.ThrowIfCancellationRequested();
            await SearchAsync();

            if (CanUpdateUi)
            {
                _notificationService.ShowSuccess(
                    archive ? "Đã lưu trữ văn bản." : "Đã khôi phục văn bản.",
                    "Cập nhật trạng thái");
            }
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                return;
            }

            if (CanUpdateUi)
            {
                _notificationService.ShowError("Không thể cập nhật trạng thái: " + ex.Message, "Lỗi workflow");
            }
        }
    }

    private void OpenCreateForm()
    {
        if (!CanUpdateUi)
        {
            return;
        }

        if (!CanCreateDocument)
        {
            _notificationService.ShowWarning("Bạn không có quyền tạo văn bản.", "Từ chối thao tác");
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
                _ = RefreshAfterCreateAsync();
            }
        }
        catch (Exception ex)
        {
            if (CanUpdateUi)
            {
                _notificationService.ShowError("Không thể mở form tạo văn bản: " + ex.Message, "Lỗi");
            }
        }
    }

    private async Task RefreshAfterCreateAsync()
    {
        await SearchAsync();
        if (CanUpdateUi)
        {
            _notificationService.ShowSuccess("Văn bản mới đã được tạo.", "Tạo văn bản");
        }
    }

    private async Task ExportToExcelAsync()
    {
        if (!CanUpdateUi)
        {
            return;
        }

        if (!CanExportExcel)
        {
            _notificationService.ShowWarning("Bạn không có quyền xuất Excel.", "Từ chối thao tác");
            return;
        }

        if (TotalCount == 0)
        {
            _notificationService.ShowWarning("Không có dữ liệu để xuất Excel.", "Không có dữ liệu");
            return;
        }

        if (TotalCount > MaxClientExportRows)
        {
            _notificationService.ShowWarning(
                $"Bộ lọc hiện tại có {TotalCount:N0} văn bản. Xuất Excel phía client đang giới hạn {MaxClientExportRows:N0} dòng để tránh quá tải bộ nhớ. Hãy thu hẹp bộ lọc hoặc dùng xuất báo cáo phía máy chủ khi có sẵn.",
                "Giới hạn xuất Excel");
            return;
        }

        try
        {
            using var _ = _operationTracker.Begin();
            _exportCts?.Dispose();
            var exportCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
            _exportCts = exportCts;

            StatusMessage = "Đang xuất Excel theo bộ lọc hiện tại...";

            var progress = new Progress<int>(count =>
            {
                if (CanUpdateUi)
                {
                    StatusMessage = $"Đã chuẩn bị {count:N0}/{TotalCount:N0} văn bản để xuất Excel...";
                }
            });

            var exportedRows = await _exportService.ExportFilteredAsync(
                CreateSearchCriteria(),
                exportCts.Token,
                progress);

            if (CanUpdateUi && exportedRows.HasValue)
            {
                StatusMessage = "Xuất Excel thành công";
                _notificationService.ShowSuccess($"Đã xuất {exportedRows.Value:N0} văn bản theo bộ lọc hiện tại.", "Xuất Excel");
            }
        }
        catch (OperationCanceledException)
        {
            if (CanUpdateUi)
            {
                StatusMessage = "Xuất Excel đã hủy";
            }
        }
        catch (Exception ex)
        {
            if (!CanUpdateUi)
            {
                return;
            }

            StatusMessage = "Xuất Excel thất bại";
            _notificationService.ShowError("Lỗi xuất Excel: " + ex.Message, "Lỗi");
        }
        finally
        {
            var exportCts = _exportCts;
            _exportCts = null;
            exportCts?.Dispose();
        }
    }

    private void SetAllBatchSelection(bool isSelected)
    {
        foreach (var row in Documents)
        {
            row.IsBatchSelected = isSelected;
        }

        RefreshBatchState();
    }

    private void SelectRowParameter(object? parameter)
    {
        if (parameter is DocumentRowViewModel row)
        {
            SelectedDocument = row;
        }
    }

    private DocumentRowViewModel? GetCommandRow(object? parameter)
    {
        return parameter as DocumentRowViewModel ?? SelectedDocument;
    }

    private void RefreshPermissions()
    {
        OnPropertyChanged(nameof(CanCreateDocument));
        OnPropertyChanged(nameof(CanEditDocument));
        OnPropertyChanged(nameof(CanViewDocument));
        OnPropertyChanged(nameof(CanExportExcel));
        RaiseCommandState();
    }

    private void RefreshBatchState()
    {
        OnPropertyChanged(nameof(BatchSelectedCount));
        OnPropertyChanged(nameof(HasBatchSelection));
        OnPropertyChanged(nameof(CanArchiveBatch));
        OnPropertyChanged(nameof(CanRestoreBatch));
        RaiseCommandState();
    }

    private void RefreshDerivedState()
    {
        OnPropertyChanged(nameof(HasDocuments));
        OnPropertyChanged(nameof(CurrentPageCount));
        OnPropertyChanged(nameof(GridStatusText));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(PageDisplayText));
        OnPropertyChanged(nameof(PreviewNextAction));
        RaiseCommandState();
    }

    private void RaiseCommandState()
    {
        OnPropertyChanged(nameof(CanOpenSelected));
        OnPropertyChanged(nameof(CanEditSelected));
        OnPropertyChanged(nameof(CanArchiveSelected));
        OnPropertyChanged(nameof(CanRestoreSelected));
        OnPropertyChanged(nameof(CanArchiveBatch));
        OnPropertyChanged(nameof(CanRestoreBatch));

        AddRelayCommand.RaiseCanExecuteChanged();
        RefreshRelayCommand.RaiseCanExecuteChanged();
        ClearFiltersRelayCommand.RaiseCanExecuteChanged();
        ExportExcelRelayCommand.RaiseCanExecuteChanged();
        PreviousPageRelayCommand.RaiseCanExecuteChanged();
        NextPageRelayCommand.RaiseCanExecuteChanged();
        OpenSelectedDocumentRelayCommand.RaiseCanExecuteChanged();
        EditSelectedDocumentRelayCommand.RaiseCanExecuteChanged();
        ArchiveSelectedRelayCommand.RaiseCanExecuteChanged();
        RestoreSelectedRelayCommand.RaiseCanExecuteChanged();
        ArchiveBatchRelayCommand.RaiseCanExecuteChanged();
        RestoreBatchRelayCommand.RaiseCanExecuteChanged();
        SelectAllBatchRelayCommand.RaiseCanExecuteChanged();
        ClearBatchRelayCommand.RaiseCanExecuteChanged();
    }
}
