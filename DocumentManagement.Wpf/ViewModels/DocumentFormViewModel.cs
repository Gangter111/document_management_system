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
    private const string ScannedImageOnlyFailureKind = "scanned_image_only";

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
    private string? _extractionMessage;
    private string _extractionMessageKind = "Info";
    private bool _hasPendingExtractionReview;
    private IReadOnlyDictionary<string, ExtractedFieldDto> _lastExtractionFields =
        new Dictionary<string, ExtractedFieldDto>();

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

    public bool CanAutoFill => HasFile && CanModifyDocument;

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
        set
        {
            if (SetProperty(ref _documentNumber, value))
                MarkReviewed("DocumentNumber");
        }
    }

    public string? ReferenceNumber
    {
        get => _referenceNumber;
        set => SetProperty(ref _referenceNumber, value);
    }

    public string Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value))
                MarkReviewed("Title");
        }
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
        set
        {
            if (SetProperty(ref _issueDate, value))
                MarkReviewed("IssueDate");
        }
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
        set
        {
            if (SetProperty(ref _senderName, value))
                MarkReviewed("SenderName");
        }
    }

    public string? ReceiverName
    {
        get => _receiverName;
        set
        {
            if (SetProperty(ref _receiverName, value))
                MarkReviewed("ReceiverName");
        }
    }

    public string? SignerName
    {
        get => _signerName;
        set
        {
            if (SetProperty(ref _signerName, value))
                MarkReviewed("SignerName");
        }
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
                ClearExtractionMessage();
                OnPropertyChanged(nameof(HasFile));
                OnPropertyChanged(nameof(CanAutoFill));
                RaiseCommandStatesChanged();
            }
        }
    }

    public bool HasFile => !string.IsNullOrWhiteSpace(SelectedFilePath);

    public string? ExtractionMessage
    {
        get => _extractionMessage;
        private set
        {
            if (SetProperty(ref _extractionMessage, value))
            {
                OnPropertyChanged(nameof(HasExtractionMessage));
            }
        }
    }

    public string ExtractionMessageKind
    {
        get => _extractionMessageKind;
        private set => SetProperty(ref _extractionMessageKind, value);
    }

    public bool HasExtractionMessage => !string.IsNullOrWhiteSpace(ExtractionMessage);

    public bool HasPendingExtractionReview
    {
        get => _hasPendingExtractionReview;
        private set => SetProperty(ref _hasPendingExtractionReview, value);
    }

    public bool DocumentNumberNeedsReview => NeedsReview("DocumentNumber");
    public bool TitleNeedsReview => NeedsReview("Title");
    public bool IssueDateNeedsReview => NeedsReview("IssueDate");
    public bool SenderNameNeedsReview => NeedsReview("SenderName");
    public bool ReceiverNameNeedsReview => NeedsReview("ReceiverName");
    public bool SignerNameNeedsReview => NeedsReview("SignerName");
    public string? DocumentNumberExtractionSource => SourceFor("DocumentNumber");
    public string? TitleExtractionSource => SourceFor("Title");
    public string? IssueDateExtractionSource => SourceFor("IssueDate");
    public string? SenderNameExtractionSource => SourceFor("SenderName");
    public string? ReceiverNameExtractionSource => SourceFor("ReceiverName");
    public string? SignerNameExtractionSource => SourceFor("SignerName");

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
        AutoFillCommand = new RelayCommand(async _ => await AutoFillFromPdfAsync(), _ => CanAutoFill);
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
        ProcessingDepartment = string.IsNullOrWhiteSpace(AuthSession.Department)
            ? null
            : AuthSession.Department;
        AssignedTo = null;
        Notes = null;
        CategoryId = null;
        StatusId = IssuedStatusId;
        SelectedFilePath = null;
        _lastExtractionFields = new Dictionary<string, ExtractedFieldDto>();
        HasPendingExtractionReview = false;
        RaiseExtractionFieldStateChanged();

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

            SetExtractionMessage(
                "Đã chọn file PDF. Bấm Trích xuất văn bản PDF để lấy thông tin từ PDF có lớp chữ.",
                "Info");
        }
    }

    private async Task AutoFillFromPdfAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            SetExtractionMessage(
                "Vui lòng chọn file PDF trước khi trích xuất văn bản.",
                "Warning");
            return;
        }

        try
        {
            SetExtractionMessage(
                "Đang trích xuất văn bản PDF. Nếu đây là PDF scan ảnh, hệ thống sẽ thử OCR cục bộ trong giới hạn an toàn.",
                "Info");
            var result = await _apiService.ExtractPdfAsync(SelectedFilePath);

            if (IsScannedImageOnly(result))
            {
                SetExtractionMessage(
                    "PDF này là tệp hợp lệ nhưng có vẻ là văn bản scan dạng ảnh.\n" +
                    "OCR cục bộ chưa khả dụng hoặc không đọc được chữ trong tệp này, nên hệ thống chưa thể điền thông tin tự động.\n" +
                    "Bạn vẫn có thể nhập thông tin văn bản thủ công.",
                    "Info");
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.FailureKind))
            {
                SetExtractionMessage(
                    GetExtractionFailureMessage(result),
                    "Warning");
                return;
            }

            if (!HasExtractedContent(result))
            {
                SetExtractionMessage(
                    "Không tìm thấy text layer trong PDF. PDF scan ảnh hiện chưa hỗ trợ OCR.",
                    "Info");
                return;
            }

            ApplyAutoFillResult(result);
            _lastExtractionFields = result.Fields.ToDictionary(field => field.FieldName, StringComparer.Ordinal);
            HasPendingExtractionReview = result.RequiresManualReview;
            RaiseExtractionFieldStateChanged();

            if (result.RequiresManualReview)
            {
                var reasons = result.ReviewReasons.Count == 0
                    ? "Một số trường OCR có độ tin cậy thấp."
                    : string.Join(" ", result.ReviewReasons);
                SetExtractionMessage(
                    "OCR đã hoàn tất nhưng cần người dùng rà soát trước khi lưu. " + reasons,
                    "Warning");
            }
            else if (result.IsFromOcr)
            {
                SetExtractionMessage(
                    "Đã trích xuất nội dung từ PDF scan bằng OCR. Vui lòng kiểm tra lại dữ liệu.",
                    result.IsPartialExtraction ? "Warning" : "Info");
            }
            else
            {
                SetExtractionMessage(
                    "Đã trích xuất văn bản từ PDF. Vui lòng kiểm tra lại các trường trước khi lưu.",
                    "Info");
            }
        }
        catch (Exception)
        {
            SetExtractionMessage(
                "Không thể trích xuất văn bản từ PDF lúc này.\n" +
                "Tệp vẫn có thể được lưu cùng hồ sơ; vui lòng nhập thông tin văn bản thủ công.",
                "Error");
        }
    }

    private void SetExtractionMessage(string message, string kind)
    {
        ExtractionMessageKind = kind;
        ExtractionMessage = message;
    }

    private void ClearExtractionMessage()
    {
        ExtractionMessage = null;
        ExtractionMessageKind = "Info";
    }

    private static string SanitizeInlineError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Vui lòng kiểm tra lại tệp hoặc nhập thông tin thủ công.";

        return message
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();
    }

    private static bool HasExtractedContent(AutoFillDocumentResultDto result)
    {
        return !string.IsNullOrWhiteSpace(result.DocumentNumber)
               || !string.IsNullOrWhiteSpace(result.Title)
               || !string.IsNullOrWhiteSpace(result.Summary)
               || (!string.IsNullOrWhiteSpace(result.ContentText) && !IsExtractionWarning(result.ContentText))
               || !string.IsNullOrWhiteSpace(result.SenderName)
               || !string.IsNullOrWhiteSpace(result.ReceiverName)
               || !string.IsNullOrWhiteSpace(result.SignerName)
               || !string.IsNullOrWhiteSpace(result.UrgencyLevel)
               || !string.IsNullOrWhiteSpace(result.IssueDate);
    }

    private static bool IsScannedImageOnly(AutoFillDocumentResultDto result)
    {
        return string.Equals(
            result.FailureKind,
            ScannedImageOnlyFailureKind,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string GetExtractionFailureMessage(AutoFillDocumentResultDto result)
    {
        return result.FailureKind?.ToLowerInvariant() switch
        {
            "encrypted" =>
                "PDF này có thể đang được mã hóa hoặc bảo vệ bằng mật khẩu.\n" +
                "Hệ thống không thể đọc tự động nội dung, nhưng bạn vẫn có thể nhập thông tin thủ công.",
            "too_large" =>
                "PDF vượt quá giới hạn xử lý tự động hiện tại.\n" +
                "Vui lòng nhập thông tin thủ công hoặc dùng tệp nhỏ hơn nếu cần trích xuất tự động.",
            "timeout" =>
                "Quá trình trích xuất PDF vượt quá thời gian cho phép.\n" +
                "Hệ thống đã dừng xử lý để bảo vệ hiệu năng; bạn vẫn có thể nhập thông tin thủ công.",
            "unsupported" =>
                "Hệ thống chỉ hỗ trợ trích xuất tự động từ tệp PDF hợp lệ.\n" +
                "Vui lòng kiểm tra lại tệp hoặc nhập thông tin thủ công.",
            _ =>
                "Hệ thống chưa thể trích xuất tự động nội dung từ PDF này.\n" +
                "Tệp vẫn có thể được lưu cùng hồ sơ; vui lòng nhập thông tin văn bản thủ công."
        };
    }

    private static bool IsExtractionWarning(string value)
    {
        return value.TrimStart().StartsWith("CẢNH BÁO:", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyAutoFillResult(AutoFillDocumentResultDto result)
    {
        if (CanApply(result, nameof(result.DocumentNumber)) && !string.IsNullOrWhiteSpace(result.DocumentNumber))
        {
            DocumentNumber = result.DocumentNumber;
        }

        if (CanApply(result, nameof(result.Title)) && !string.IsNullOrWhiteSpace(result.Title))
        {
            Title = result.Title;
        }

        if (CanApply(result, nameof(result.Title)) && !string.IsNullOrWhiteSpace(result.Summary))
        {
            Summary = result.Summary;
        }

        if (!result.IsFromOcr && !string.IsNullOrWhiteSpace(result.ContentText))
        {
            ContentText = result.ContentText;
        }

        if (CanApply(result, nameof(result.SenderName)) && !string.IsNullOrWhiteSpace(result.SenderName))
        {
            SenderName = result.SenderName;
        }

        if (CanApply(result, nameof(result.ReceiverName)) && !string.IsNullOrWhiteSpace(result.ReceiverName))
        {
            ReceiverName = result.ReceiverName;
        }

        if (CanApply(result, nameof(result.SignerName)) && !string.IsNullOrWhiteSpace(result.SignerName))
        {
            SignerName = result.SignerName;
        }

        if (!string.IsNullOrWhiteSpace(result.UrgencyLevel))
        {
            UrgencyLevel = result.UrgencyLevel;
        }

        if (CanApply(result, nameof(result.IssueDate)) && DateTime.TryParse(result.IssueDate, out var issueDate))
        {
            IssueDate = issueDate;
        }
    }

    private static bool CanApply(AutoFillDocumentResultDto result, string propertyName)
    {
        var fieldName = propertyName switch
        {
            nameof(AutoFillDocumentResultDto.DocumentNumber) => "DocumentNumber",
            nameof(AutoFillDocumentResultDto.Title) => "Title",
            nameof(AutoFillDocumentResultDto.SenderName) => "SenderName",
            nameof(AutoFillDocumentResultDto.ReceiverName) => "ReceiverName",
            nameof(AutoFillDocumentResultDto.SignerName) => "SignerName",
            nameof(AutoFillDocumentResultDto.IssueDate) => "IssueDate",
            _ => propertyName
        };

        var field = result.Fields.FirstOrDefault(candidate => candidate.FieldName == fieldName);
        return field == null || !field.RequiresReview;
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

            if (HasPendingExtractionReview)
            {
                _notificationService.ShowWarning(
                    "Có trường OCR đang cần rà soát thủ công trước khi lưu.",
                    "Cần kiểm tra OCR");
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

        RaiseCommandStatesChanged();
    }

    private void RaiseCommandStatesChanged()
    {
        if (BrowseFileCommand is RelayCommand browseFileCommand)
        {
            browseFileCommand.RaiseCanExecuteChanged();
        }

        if (AutoFillCommand is RelayCommand autoFillCommand)
        {
            autoFillCommand.RaiseCanExecuteChanged();
        }

        if (SaveCommand is RelayCommand saveCommand)
        {
            saveCommand.RaiseCanExecuteChanged();
        }

        if (DeleteCommand is RelayCommand deleteCommand)
        {
            deleteCommand.RaiseCanExecuteChanged();
        }
    }

    private bool NeedsReview(string fieldName)
        => _lastExtractionFields.TryGetValue(fieldName, out var field) && field.RequiresReview;

    private string? SourceFor(string fieldName)
        => _lastExtractionFields.TryGetValue(fieldName, out var field)
            ? $"Nguồn OCR: {field.SourceText}\nĐộ tin cậy: {field.Confidence:0.00}\nCách trích xuất: {field.ExtractionMethod}"
            : null;

    private void RaiseExtractionFieldStateChanged()
    {
        OnPropertyChanged(nameof(DocumentNumberNeedsReview));
        OnPropertyChanged(nameof(TitleNeedsReview));
        OnPropertyChanged(nameof(IssueDateNeedsReview));
        OnPropertyChanged(nameof(SenderNameNeedsReview));
        OnPropertyChanged(nameof(ReceiverNameNeedsReview));
        OnPropertyChanged(nameof(SignerNameNeedsReview));
        OnPropertyChanged(nameof(DocumentNumberExtractionSource));
        OnPropertyChanged(nameof(TitleExtractionSource));
        OnPropertyChanged(nameof(IssueDateExtractionSource));
        OnPropertyChanged(nameof(SenderNameExtractionSource));
        OnPropertyChanged(nameof(ReceiverNameExtractionSource));
        OnPropertyChanged(nameof(SignerNameExtractionSource));
    }

    private void MarkReviewed(string fieldName)
    {
        if (!_lastExtractionFields.TryGetValue(fieldName, out var field) || !field.RequiresReview)
            return;

        var updated = _lastExtractionFields.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        updated[fieldName] = new ExtractedFieldDto
        {
            FieldName = field.FieldName,
            Value = field.Value,
            Confidence = field.Confidence,
            SourceText = field.SourceText,
            ExtractionMethod = field.ExtractionMethod,
            RequiresReview = false
        };
        _lastExtractionFields = updated;
        HasPendingExtractionReview = _lastExtractionFields.Values.Any(candidate => candidate.RequiresReview);
        RaiseExtractionFieldStateChanged();
    }
}
