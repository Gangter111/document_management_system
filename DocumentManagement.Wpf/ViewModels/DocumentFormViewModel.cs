using System;
using DocumentManagement.Domain.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using DocumentManagement.Application.Security;
using DocumentManagement.Domain.Entities;
using DocumentManagement.Wpf.Commands;
using DocumentManagement.Wpf.Services;
using Microsoft.Win32;

namespace DocumentManagement.Wpf.ViewModels;

public class DocumentFormViewModel : BaseViewModel
{
    private static long DraftStatusId => (long)DocumentStatus.Draft;
    private static long PendingStatusId => (long)DocumentStatus.PendingApproval;
    private static long RejectedStatusId => (long)DocumentStatus.Rejected;

    private readonly IDocumentService _documentService;
    private readonly IOcrService _ocrService;
    private readonly IAttachmentService _attachmentService;
    private readonly IAuthService _authService;
    private readonly IPermissionService _permissionService;
    private readonly IHistoryRepository _historyRepository;
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
    private long? _statusId = DraftStatusId;

    private bool _lookupsLoaded;
    private bool _isReadOnlyMode;

    public bool IsEditMode => _id > 0;

    public bool IsReadOnlyMode
    {
        get => _isReadOnlyMode;
        private set => SetProperty(ref _isReadOnlyMode, value);
    }

    public List<DocumentHistoryModel> Histories { get; private set; } = new();

    public bool HasHistories => Histories.Count > 0;

    public bool CanViewDocument =>
        _permissionService.HasPermission(_authService.CurrentUser, PermissionCodes.DocumentView);

    public bool CanCreateDocument =>
        _permissionService.HasPermission(_authService.CurrentUser, PermissionCodes.DocumentCreate);

    public bool CanEditDocument =>
        _permissionService.HasPermission(_authService.CurrentUser, PermissionCodes.DocumentEdit);

    public bool CanSubmitDocument =>
        _permissionService.HasPermission(_authService.CurrentUser, PermissionCodes.DocumentSubmit);

    public bool CanApproveDocument =>
        _permissionService.HasPermission(_authService.CurrentUser, PermissionCodes.DocumentApprove);

    public bool CanRejectDocument =>
        _permissionService.HasPermission(_authService.CurrentUser, PermissionCodes.DocumentReject);

    public bool CanModifyDocument =>
        !IsReadOnlyMode
        && (!IsEditMode ? CanCreateDocument : CanEditDocument)
        && (!IsEditMode || StatusId == DraftStatusId || StatusId == RejectedStatusId);

    public bool CanBrowseFile => CanModifyDocument;

    public bool CanAutoFill => HasFile && CanModifyDocument;

    public bool CanSaveDocument => CanModifyDocument;

    public bool CanSaveAndSubmitForApproval =>
        !IsReadOnlyMode
        && CanSubmitDocument
        && CanModifyDocument
        && (!IsEditMode || StatusId == DraftStatusId || StatusId == RejectedStatusId);

    public bool CanSubmitForApproval =>
        IsEditMode
        && !IsReadOnlyMode
        && CanSubmitDocument
        && (StatusId == DraftStatusId || StatusId == RejectedStatusId);

    public bool CanApprove =>
        IsEditMode
        && !IsReadOnlyMode
        && CanApproveDocument
        && StatusId == PendingStatusId;

    public bool CanReject =>
        IsEditMode
        && !IsReadOnlyMode
        && CanRejectDocument
        && StatusId == PendingStatusId;

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

    public List<CategoryModel> Categories { get; private set; } = new();
    public List<StatusModel> Statuses { get; private set; } = new();

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

    public bool HasFile => !string.IsNullOrEmpty(SelectedFilePath);

    public ICommand BrowseFileCommand { get; }
    public ICommand AutoFillCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAndSubmitForApprovalCommand { get; }
    public ICommand SubmitForApprovalCommand { get; }
    public ICommand ApproveCommand { get; }
    public ICommand RejectCommand { get; }

    public DocumentFormViewModel(
        IDocumentService documentService,
        IOcrService ocrService,
        IAttachmentService attachmentService,
        IAuthService authService,
        IPermissionService permissionService,
        IHistoryRepository historyRepository,
        INotificationService notificationService,
        IConfirmDialogService confirmDialogService)
    {
        _documentService = documentService;
        _ocrService = ocrService;
        _attachmentService = attachmentService;
        _authService = authService;
        _permissionService = permissionService;
        _historyRepository = historyRepository;
        _notificationService = notificationService;
        _confirmDialogService = confirmDialogService;

        BrowseFileCommand = new RelayCommand(_ => BrowseFile(), _ => CanBrowseFile);
        AutoFillCommand = new RelayCommand(async _ => await RunAutoFillAsync(), _ => CanAutoFill);
        SaveCommand = new RelayCommand(async w => await SaveAsync(w), _ => CanSaveDocument);
        SaveAndSubmitForApprovalCommand = new RelayCommand(async w => await SaveAndSubmitForApprovalAsync(w), _ => CanSaveAndSubmitForApproval);

        SubmitForApprovalCommand = new RelayCommand(async _ => await SubmitForApprovalAsync(), _ => CanSubmitForApproval);
        ApproveCommand = new RelayCommand(async _ => await ApproveAsync(), _ => CanApprove);
        RejectCommand = new RelayCommand(async _ => await RejectAsync(), _ => CanReject);

        _ = EnsureLookupsLoadedAsync();
    }

    public void ApplyAccessMode(bool isReadOnly)
    {
        IsReadOnlyMode = isReadOnly;
        RefreshAccessState();
    }

    private async Task EnsureLookupsLoadedAsync()
    {
        if (_lookupsLoaded)
        {
            return;
        }

        try
        {
            Categories = await _documentService.GetCategoriesAsync();
            Statuses = await _documentService.GetStatusesAsync();

            _lookupsLoaded = true;

            OnPropertyChanged(nameof(Categories));
            OnPropertyChanged(nameof(Statuses));

            if (_id == 0 && !StatusId.HasValue)
            {
                StatusId = DraftStatusId;
            }

            if (_id == 0 && Statuses.Count > 0)
            {
                var draft = Statuses.FirstOrDefault(x =>
                    string.Equals(x.Code, "DRAFT", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.Name, "Bản nháp", StringComparison.OrdinalIgnoreCase));

                if (draft != null)
                {
                    StatusId = draft.Id;
                }
            }
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                $"Không thể tải dữ liệu danh mục/trạng thái: {ex.Message}",
                "Lỗi dữ liệu");
        }
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

        await EnsureLookupsLoadedAsync();

        var doc = await _documentService.GetByIdAsync(id);

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
        ConfidentialityLevel = doc.ConfidentialityLevel;
        UrgencyLevel = doc.UrgencyLevel;
        SenderName = doc.SenderName;
        ReceiverName = doc.ReceiverName;
        SignerName = doc.SignerName;
        ProcessingDepartment = doc.ProcessingDepartment;
        AssignedTo = doc.AssignedTo;
        Notes = doc.Notes;
        CategoryId = doc.CategoryId;
        StatusId = doc.StatusId;
        SelectedFilePath = null;

        IssueDate = DateTime.TryParse(doc.IssueDate, out var issueDate) ? issueDate : null;
        ReceivedDate = DateTime.TryParse(doc.ReceivedDate, out var receivedDate) ? receivedDate : null;
        DueDate = DateTime.TryParse(doc.DueDate, out var dueDate) ? dueDate : null;

        await LoadHistoryAsync();

        OnPropertyChanged(nameof(IsEditMode));
        RefreshAccessState();
    }

    private async Task LoadHistoryAsync()
    {
        Histories = _id <= 0
            ? new List<DocumentHistoryModel>()
            : await _historyRepository.GetByDocumentIdAsync(_id);

        OnPropertyChanged(nameof(Histories));
        OnPropertyChanged(nameof(HasHistories));
    }

    private void BrowseFile()
    {
        if (!CanBrowseFile)
        {
            _notificationService.ShowWarning(
                "Bạn không có quyền thao tác với tệp đính kèm.",
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
                "Đã chọn file PDF. Bạn có thể tự động điền dữ liệu từ file này.",
                "Đã chọn tệp");
        }
    }

    private async Task RunAutoFillAsync()
    {
        if (!CanAutoFill)
        {
            _notificationService.ShowWarning(
                "Bạn không có quyền tự động điền dữ liệu văn bản.",
                "Từ chối thao tác");
            return;
        }

        if (string.IsNullOrEmpty(SelectedFilePath))
        {
            _notificationService.ShowWarning(
                "Vui lòng chọn file PDF trước khi tự động điền.",
                "Thiếu tệp");
            return;
        }

        try
        {
            var result = await _ocrService.ExtractAndParseAsync(SelectedFilePath);

            if (!string.IsNullOrEmpty(result.DocumentNumber))
                DocumentNumber = result.DocumentNumber;

            if (!string.IsNullOrEmpty(result.Title))
                Title = result.Title;

            if (!string.IsNullOrEmpty(result.Summary))
                Summary = result.Summary;

            if (!string.IsNullOrEmpty(result.ContentText))
                ContentText = result.ContentText;

            if (!string.IsNullOrEmpty(result.UrgencyLevel))
                UrgencyLevel = result.UrgencyLevel;

            if (!string.IsNullOrEmpty(result.SenderName))
                SenderName = result.SenderName;

            if (!string.IsNullOrEmpty(result.IssueDate) &&
                DateTime.TryParse(result.IssueDate, out var date))
            {
                IssueDate = date;
            }

            _notificationService.ShowSuccess(
                "Đã tự động điền dữ liệu từ file PDF.",
                "OCR hoàn tất");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                $"Lỗi trích xuất: {ex.Message}",
                "Lỗi OCR");
        }
    }

    private async Task SaveAsync(object? parameter)
    {
        var docId = await SaveCoreAsync(showSuccess: true);

        if (!docId.HasValue)
        {
            return;
        }

        if (parameter is Window window)
        {
            window.DialogResult = true;
            window.Close();
        }
    }

    private async Task SaveAndSubmitForApprovalAsync(object? parameter)
    {
        if (!CanSaveAndSubmitForApproval)
        {
            _notificationService.ShowWarning(
                "Bạn không có quyền lưu và gửi duyệt văn bản.",
                "Từ chối thao tác");
            return;
        }

        var confirmed = _confirmDialogService.Confirm(
            "Lưu văn bản và gửi duyệt ngay?",
            "Lưu & gửi duyệt",
            "Lưu & gửi",
            "Hủy",
            ConfirmDialogType.Warning);

        if (!confirmed)
        {
            return;
        }

        var docId = await SaveCoreAsync(showSuccess: false);

        if (!docId.HasValue)
        {
            return;
        }

        try
        {
            await _documentService.SubmitForApprovalAsync(docId.Value);

            _id = docId.Value;

            var reloaded = await _documentService.GetByIdAsync(_id);

            if (reloaded != null)
            {
                StatusId = reloaded.StatusId;
            }

            await LoadHistoryAsync();

            _notificationService.ShowSuccess(
                "Văn bản đã được lưu và gửi duyệt.",
                "Gửi duyệt thành công");

            RefreshAccessState();

            if (parameter is Window window)
            {
                window.DialogResult = true;
                window.Close();
            }
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                ex.Message,
                "Lỗi gửi duyệt");
        }
    }

    private async Task<long?> SaveCoreAsync(bool showSuccess)
    {
        try
        {
            if (!CanSaveDocument)
            {
                _notificationService.ShowWarning(
                    IsEditMode
                        ? "Không thể sửa văn bản ở trạng thái hiện tại."
                        : "Bạn không có quyền tạo mới văn bản.",
                    "Từ chối thao tác");
                return null;
            }

            if (string.IsNullOrWhiteSpace(DocumentNumber))
            {
                _notificationService.ShowWarning(
                    "Vui lòng nhập số hiệu văn bản.",
                    "Thiếu dữ liệu");
                return null;
            }

            if (string.IsNullOrWhiteSpace(Title))
            {
                _notificationService.ShowWarning(
                    "Vui lòng nhập tiêu đề / trích yếu văn bản.",
                    "Thiếu dữ liệu");
                return null;
            }

            var username = _authService.CurrentUser?.Username ?? "system";
            var finalStatusId = StatusId ?? DraftStatusId;

            var doc = new Document
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
                StatusId = finalStatusId,
                CreatedBy = username,
                UpdatedBy = username
            };

            long docId;

            if (_id > 0)
            {
                doc.SetUpdated(DateTime.UtcNow, username);
                await _documentService.UpdateAsync(doc);
                docId = _id;
            }
            else
            {
                doc.SetCreated(DateTime.UtcNow, username);
                docId = await _documentService.CreateAsync(doc);
                _id = docId;

                OnPropertyChanged(nameof(IsEditMode));
            }

            if (!string.IsNullOrEmpty(SelectedFilePath))
            {
                await _attachmentService.UploadAsync(docId, SelectedFilePath);
                SelectedFilePath = null;
            }

            await LoadHistoryAsync();

            if (showSuccess)
            {
                _notificationService.ShowSuccess(
                    IsEditMode ? "Đã cập nhật văn bản." : "Đã tạo mới văn bản.",
                    "Lưu thành công");
            }

            RefreshAccessState();

            return docId;
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                ex.Message,
                "Lỗi khi lưu");
            return null;
        }
    }

    private async Task SubmitForApprovalAsync()
    {
        if (_id <= 0)
        {
            _notificationService.ShowWarning(
                "Bạn cần lưu văn bản trước khi gửi duyệt.",
                "Thông báo");
            return;
        }

        await RunWorkflowActionAsync(
            "Gửi văn bản này để duyệt?",
            "Đã gửi duyệt văn bản.",
            () => _documentService.SubmitForApprovalAsync(_id),
            "Gửi duyệt",
            ConfirmDialogType.Warning);
    }

    private async Task ApproveAsync()
    {
        await RunWorkflowActionAsync(
            "Phê duyệt văn bản này?",
            "Đã phê duyệt văn bản.",
            () => _documentService.ApproveAsync(_id),
            "Duyệt",
            ConfirmDialogType.Info);
    }

    private async Task RejectAsync()
    {
        var reason = Notes;

        if (string.IsNullOrWhiteSpace(reason))
        {
            var continueWithoutReason = _confirmDialogService.Confirm(
                "Bạn chưa nhập lý do từ chối trong ô Ghi chú.\n\nBạn vẫn muốn tiếp tục từ chối văn bản này?",
                "Xác nhận từ chối",
                "Tiếp tục",
                "Hủy",
                ConfirmDialogType.Warning);

            if (!continueWithoutReason)
            {
                return;
            }
        }

        await RunWorkflowActionAsync(
            "Từ chối văn bản này?",
            "Đã từ chối văn bản.",
            () => _documentService.RejectAsync(_id, reason),
            "Từ chối",
            ConfirmDialogType.Danger);
    }

    private async Task RunWorkflowActionAsync(
        string confirmMessage,
        string successMessage,
        Func<Task> action,
        string confirmText,
        ConfirmDialogType dialogType)
    {
        try
        {
            var confirmed = _confirmDialogService.Confirm(
                confirmMessage,
                "Xác nhận",
                confirmText,
                "Hủy",
                dialogType);

            if (!confirmed)
            {
                return;
            }

            await action();

            var reloaded = await _documentService.GetByIdAsync(_id);

            if (reloaded != null)
            {
                StatusId = reloaded.StatusId;
            }

            await LoadHistoryAsync();

            _notificationService.ShowSuccess(
                successMessage,
                "Workflow");

            RefreshAccessState();
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                ex.Message,
                "Lỗi workflow");
        }
    }

    private void RefreshAccessState()
    {
        OnPropertyChanged(nameof(IsReadOnlyMode));
        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(CanViewDocument));
        OnPropertyChanged(nameof(CanCreateDocument));
        OnPropertyChanged(nameof(CanEditDocument));
        OnPropertyChanged(nameof(CanSubmitDocument));
        OnPropertyChanged(nameof(CanApproveDocument));
        OnPropertyChanged(nameof(CanRejectDocument));
        OnPropertyChanged(nameof(CanModifyDocument));
        OnPropertyChanged(nameof(CanBrowseFile));
        OnPropertyChanged(nameof(CanAutoFill));
        OnPropertyChanged(nameof(CanSaveDocument));
        OnPropertyChanged(nameof(CanSaveAndSubmitForApproval));
        OnPropertyChanged(nameof(CanSubmitForApproval));
        OnPropertyChanged(nameof(CanApprove));
        OnPropertyChanged(nameof(CanReject));

        CommandManager.InvalidateRequerySuggested();
    }
}