using System.Windows;
using System.Windows.Input;
using DocumentManagement.Contracts.Documents;
using DocumentManagement.Wpf.Commands;
using DocumentManagement.Wpf.Services;
using Microsoft.Win32;

namespace DocumentManagement.Wpf.ViewModels;

public class DocumentFormViewModel : BaseViewModel
{
    private const long IssuedStatusId = 4;

    private readonly ApiService _apiService;
    private readonly ApiAuthService _authService;
    private readonly ClientPermissionService _permissionService;
    private readonly INotificationService _notificationService;
    private readonly IConfirmDialogService _confirmDialogService;

    private long _id;
    private string _documentType = "INCOMING";
    private string _documentNumber = string.Empty;
    private string? _referenceNumber;
    private string _title = string.Empty;
    private string? _summary;
    private string? _contentText;
    private DateTime? _issueDate = DateTime.Today;
    private DateTime? _receivedDate = DateTime.Today;
    private DateTime? _dueDate;
    private string? _senderName;
    private string? _receiverName;
    private string? _signerName;
    private string _confidentialityLevel = "NORMAL";
    private string _urgencyLevel = "NORMAL";
    private string? _processingDepartment;
    private string? _assignedTo;
    private string? _notes;
    private string? _selectedFilePath;

    private long? _categoryId;
    private long? _statusId = IssuedStatusId;

    private bool _isReadOnlyMode;

    public bool IsEditMode => _id > 0;

    public bool IsReadOnlyMode
    {
        get => _isReadOnlyMode;
        private set => SetProperty(ref _isReadOnlyMode, value);
    }

    public bool CanViewDocument => _permissionService.CanViewDocuments();

    public bool CanCreateDocument => _permissionService.CanCreateDocuments();

    public bool CanEditDocument => _permissionService.CanEditDocuments();

    public bool CanDeleteDocument => _permissionService.CanDeleteDocuments();

    public bool CanModifyDocument =>
        !IsReadOnlyMode
        && (!IsEditMode ? CanCreateDocument : CanEditDocument);

    public bool CanBrowseFile => CanModifyDocument;

    public bool CanAutoFill => false;

    public bool CanSaveDocument => CanModifyDocument;

    public bool CanDelete =>
        IsEditMode
        && !IsReadOnlyMode
        && CanDeleteDocument;

    public string DocumentType
    {
        get => _documentType;
        set => SetProperty(ref _documentType, value);
    }

    public string DocumentNumber
    {
        get => _documentNumber;
        set => SetProperty(ref _documentNumber, value);
    }

    public string? ReferenceNumber
    {
        get => _referenceNumber;
        set => SetProperty(ref _referenceNumber, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string? Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }

    public string? ContentText
    {
        get => _contentText;
        set => SetProperty(ref _contentText, value);
    }

    public DateTime? IssueDate
    {
        get => _issueDate;
        set => SetProperty(ref _issueDate, value);
    }

    public DateTime? ReceivedDate
    {
        get => _receivedDate;
        set => SetProperty(ref _receivedDate, value);
    }

    public DateTime? DueDate
    {
        get => _dueDate;
        set => SetProperty(ref _dueDate, value);
    }

    public string? SenderName
    {
        get => _senderName;
        set => SetProperty(ref _senderName, value);
    }

    public string? ReceiverName
    {
        get => _receiverName;
        set => SetProperty(ref _receiverName, value);
    }

    public string? SignerName
    {
        get => _signerName;
        set => SetProperty(ref _signerName, value);
    }

    public string ConfidentialityLevel
    {
        get => _confidentialityLevel;
        set => SetProperty(ref _confidentialityLevel, value);
    }

    public string UrgencyLevel
    {
        get => _urgencyLevel;
        set => SetProperty(ref _urgencyLevel, value);
    }

    public string? ProcessingDepartment
    {
        get => _processingDepartment;
        set => SetProperty(ref _processingDepartment, value);
    }

    public string? AssignedTo
    {
        get => _assignedTo;
        set => SetProperty(ref _assignedTo, value);
    }

    public string? Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public long? CategoryId
    {
        get => _categoryId;
        set => SetProperty(ref _categoryId, value);
    }

    public long? StatusId
    {
        get => _statusId;
        set
        {
            if (SetProperty(ref _statusId, value))
            {
                RefreshAccessState();
            }
        }
    }

    public string? SelectedFilePath
    {
        get => _selectedFilePath;
        set
        {
            if (SetProperty(ref _selectedFilePath, value))
            {
                OnPropertyChanged(nameof(HasFile));
                OnPropertyChanged(nameof(CanAutoFill));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool HasFile => !string.IsNullOrWhiteSpace(SelectedFilePath);

    public ICommand BrowseFileCommand { get; }

    public ICommand AutoFillCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand DeleteCommand { get; }

    public DocumentFormViewModel(
        ApiService apiService,
        ApiAuthService authService,
        ClientPermissionService permissionService,
        INotificationService notificationService,
        IConfirmDialogService confirmDialogService)
    {
        _apiService = apiService;
        _authService = authService;
        _permissionService = permissionService;
        _notificationService = notificationService;
        _confirmDialogService = confirmDialogService;

        BrowseFileCommand = new RelayCommand(_ => BrowseFile(), _ => CanBrowseFile);
        AutoFillCommand = new RelayCommand(_ => ShowOcrDisabledMessage(), _ => false);
        SaveCommand = new RelayCommand(async w => await SaveAsync(w), _ => CanSaveDocument);
        DeleteCommand = new RelayCommand(async w => await DeleteAsync(w), _ => CanDelete);
    }

    public void ResetForCreate()
    {
        _id = 0;

        DocumentType = "INCOMING";
        DocumentNumber = string.Empty;
        ReferenceNumber = null;
        Title = string.Empty;
        Summary = null;
        ContentText = null;
        IssueDate = DateTime.Today;
        ReceivedDate = DateTime.Today;
        DueDate = null;
        SenderName = null;
        ReceiverName = null;
        SignerName = null;
        ConfidentialityLevel = "NORMAL";
        UrgencyLevel = "NORMAL";
        ProcessingDepartment = null;
        AssignedTo = null;
        Notes = null;
        CategoryId = null;
        StatusId = IssuedStatusId;
        SelectedFilePath = null;

        OnPropertyChanged(nameof(IsEditMode));
        RefreshAccessState();
    }

    public void ApplyAccessMode(bool isReadOnly)
    {
        IsReadOnlyMode = isReadOnly;
        RefreshAccessState();
    }

    public async Task LoadDocumentAsync(long id)
    {
        if (!CanViewDocument)
        {
            _notificationService.ShowWarning(
                "Bạn không có quyền xem văn bản.",
                "Từ chối truy cập");
            return;
        }

        try
        {
            var doc = await _apiService.GetDocumentByIdAsync(id);

            if (doc == null)
            {
                _notificationService.ShowInfo(
                    "Không tìm thấy văn bản.",
                    "Thông báo");
                return;
            }

            _id = doc.Id;

            DocumentType = doc.DocumentType;
            DocumentNumber = doc.DocumentNumber;
            ReferenceNumber = doc.ReferenceNumber;
            Title = doc.Title;
            Summary = doc.Summary;
            ContentText = doc.ContentText;

            IssueDate = DateTime.TryParse(doc.IssueDate, out var issueDate) ? issueDate : null;
            ReceivedDate = DateTime.TryParse(doc.ReceivedDate, out var receivedDate) ? receivedDate : null;
            DueDate = DateTime.TryParse(doc.DueDate, out var dueDate) ? dueDate : null;

            SenderName = doc.SenderName;
            ReceiverName = doc.ReceiverName;
            SignerName = doc.SignerName;
            ConfidentialityLevel = doc.ConfidentialityLevel;
            UrgencyLevel = doc.UrgencyLevel;
            ProcessingDepartment = doc.ProcessingDepartment;
            AssignedTo = doc.AssignedTo;
            Notes = doc.Notes;
            CategoryId = doc.CategoryId;
            StatusId = doc.StatusId;
            SelectedFilePath = null;

            OnPropertyChanged(nameof(IsEditMode));
            RefreshAccessState();
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                "Không thể tải văn bản từ API: " + ex.Message,
                "Lỗi API");
        }
    }

    private void BrowseFile()
    {
        if (!CanBrowseFile)
        {
            _notificationService.ShowWarning(
                "Bạn không có quyền thao tác với tệp.",
                "Từ chối thao tác");
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedFilePath = dialog.FileName;

            _notificationService.ShowInfo(
                "Đã chọn file PDF. OCR client đã tạm tắt, cần chuyển OCR sang API.",
                "Đã chọn tệp");
        }
    }

    private void ShowOcrDisabledMessage()
    {
        _notificationService.ShowInfo(
            "OCR local đã bị tắt để giữ đúng kiến trúc client-server. Cần làm OCR qua API.",
            "OCR chưa khả dụng");
    }

    private async Task SaveAsync(object? parameter)
    {
        var saved = await SaveCoreAsync();

        if (!saved)
        {
            return;
        }

        if (parameter is Window window)
        {
            window.DialogResult = true;
            window.Close();
        }
    }

    private async Task<bool> SaveCoreAsync()
    {
        try
        {
            if (!CanSaveDocument)
            {
                _notificationService.ShowWarning(
                    IsEditMode
                        ? "Bạn không có quyền sửa văn bản."
                        : "Bạn không có quyền tạo văn bản.",
                    "Từ chối thao tác");
                return false;
            }

            if (string.IsNullOrWhiteSpace(DocumentNumber))
            {
                _notificationService.ShowWarning(
                    "Vui lòng nhập số hiệu văn bản.",
                    "Thiếu dữ liệu");
                return false;
            }

            if (string.IsNullOrWhiteSpace(Title))
            {
                _notificationService.ShowWarning(
                    "Vui lòng nhập tiêu đề / trích yếu văn bản.",
                    "Thiếu dữ liệu");
                return false;
            }

            var username = _authService.CurrentUser?.Username;

            if (string.IsNullOrWhiteSpace(username))
            {
                username = Environment.UserName;
            }

            var statusId = StatusId is > 0 ? StatusId.Value : IssuedStatusId;

            if (_id > 0)
            {
                var request = new UpdateDocumentRequest
                {
                    Id = _id,
                    DocumentType = DocumentType,
                    DocumentNumber = DocumentNumber,
                    ReferenceNumber = ReferenceNumber,
                    Title = Title,
                    Summary = Summary,
                    ContentText = ContentText,
                    IssueDate = IssueDate?.ToString("yyyy-MM-dd"),
                    ReceivedDate = ReceivedDate?.ToString("yyyy-MM-dd"),
                    DueDate = DueDate?.ToString("yyyy-MM-dd"),
                    SenderName = SenderName,
                    ReceiverName = ReceiverName,
                    SignerName = SignerName,
                    ConfidentialityLevel = ConfidentialityLevel,
                    UrgencyLevel = UrgencyLevel,
                    ProcessingDepartment = ProcessingDepartment,
                    AssignedTo = AssignedTo,
                    Notes = Notes,
                    CategoryId = CategoryId,
                    StatusId = statusId,
                    OcrStatus = "PENDING",
                    UpdatedBy = username
                };

                await _apiService.UpdateDocumentAsync(_id, request);

                _notificationService.ShowSuccess(
                    "Đã cập nhật văn bản.",
                    "Lưu thành công");
            }
            else
            {
                var request = new CreateDocumentRequest
                {
                    DocumentType = DocumentType,
                    DocumentNumber = DocumentNumber,
                    ReferenceNumber = ReferenceNumber,
                    Title = Title,
                    Summary = Summary,
                    ContentText = ContentText,
                    IssueDate = IssueDate?.ToString("yyyy-MM-dd"),
                    ReceivedDate = ReceivedDate?.ToString("yyyy-MM-dd"),
                    DueDate = DueDate?.ToString("yyyy-MM-dd"),
                    SenderName = SenderName,
                    ReceiverName = ReceiverName,
                    SignerName = SignerName,
                    ConfidentialityLevel = ConfidentialityLevel,
                    UrgencyLevel = UrgencyLevel,
                    ProcessingDepartment = ProcessingDepartment,
                    AssignedTo = AssignedTo,
                    Notes = Notes,
                    CategoryId = CategoryId,
                    StatusId = statusId,
                    OcrStatus = "PENDING",
                    CreatedBy = username
                };

                var newId = await _apiService.CreateDocumentAsync(request);
                _id = newId;

                OnPropertyChanged(nameof(IsEditMode));

                _notificationService.ShowSuccess(
                    "Đã tạo mới văn bản.",
                    "Lưu thành công");
            }

            SelectedFilePath = null;

            RefreshAccessState();
            return true;
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                "Lỗi lưu văn bản qua API: " + ex.Message,
                "Lỗi API");
            return false;
        }
    }

    private async Task DeleteAsync(object? parameter)
    {
        if (!CanDelete)
        {
            _notificationService.ShowWarning(
                "Bạn không có quyền xóa văn bản này.",
                "Từ chối thao tác");
            return;
        }

        var confirmed = _confirmDialogService.Confirm(
            "Xóa văn bản này? Thao tác này không thể hoàn tác.",
            "Xác nhận xóa",
            "Xóa",
            "Hủy",
            ConfirmDialogType.Danger);

        if (!confirmed)
        {
            return;
        }

        try
        {
            await _apiService.DeleteDocumentAsync(_id);

            _notificationService.ShowSuccess(
                "Đã xóa văn bản.",
                "Xóa thành công");

            if (parameter is Window window)
            {
                window.DialogResult = true;
                window.Close();
            }
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                "Lỗi xóa văn bản qua API: " + ex.Message,
                "Lỗi API");
        }
    }

    private void RefreshAccessState()
    {
        OnPropertyChanged(nameof(IsReadOnlyMode));
        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(CanViewDocument));
        OnPropertyChanged(nameof(CanCreateDocument));
        OnPropertyChanged(nameof(CanEditDocument));
        OnPropertyChanged(nameof(CanDeleteDocument));
        OnPropertyChanged(nameof(CanModifyDocument));
        OnPropertyChanged(nameof(CanBrowseFile));
        OnPropertyChanged(nameof(CanAutoFill));
        OnPropertyChanged(nameof(CanSaveDocument));
        OnPropertyChanged(nameof(CanDelete));

        CommandManager.InvalidateRequerySuggested();
    }
}