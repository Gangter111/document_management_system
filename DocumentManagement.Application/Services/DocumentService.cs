using System.Text.Json;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using DocumentManagement.Domain.Entities;

namespace DocumentManagement.Application.Services;

public class DocumentService : IDocumentService
{
    private const string EntityName = "Document";

    private readonly IDocumentRepository _documentRepository;
    private readonly IHistoryRepository _historyRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IAuthService _authService;

    public DocumentService(
        IDocumentRepository documentRepository,
        IHistoryRepository historyRepository,
        IAuditLogRepository auditLogRepository,
        IAuthService authService)
    {
        _documentRepository = documentRepository;
        _historyRepository = historyRepository;
        _auditLogRepository = auditLogRepository;
        _authService = authService;
    }

    public async Task<IEnumerable<Document>> GetAllAsync()
    {
        var all = await _documentRepository.GetAllAsync();
        return all.Where(x => x.IsActive);
    }

    public async Task<IEnumerable<Document>> SearchAsync(DocumentSearchRequest request)
    {
        var items = await _documentRepository.SearchAsync(request);
        return items.Where(x => x.IsActive);
    }

    public async Task<PagedResult<Document>> SearchPagedAsync(DocumentSearchRequest request)
    {
        var allItems = await _documentRepository.SearchAsync(request);

        var activeItems = allItems
            .Where(x => x.IsActive)
            .ToList();

        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 100 : request.PageSize;

        return new PagedResult<Document>
        {
            Items = activeItems
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList(),
            TotalCount = activeItems.Count,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<Document?> GetByIdAsync(long id)
    {
        var document = await _documentRepository.GetByIdAsync(id);

        if (document == null || !document.IsActive)
        {
            return null;
        }

        return document;
    }

    public async Task<long> CreateAsync(Document document)
    {
        var username = GetCurrentUsername(document.CreatedBy);

        document.SetCreated(DateTime.UtcNow, username);
        document.IsActive = true;

        if (document.StatusId is null or <= 0)
        {
            document.StatusId = 1;
        }

        var id = await _documentRepository.CreateAsync(document);
        document.Id = id;

        await AddHistoryAsync(
            id,
            "CREATE",
            $"Tạo mới văn bản số: {document.DocumentNumber}",
            username);

        await AddAuditAsync(
            id,
            "CREATE",
            null,
            document,
            username);

        return id;
    }

    public async Task UpdateAsync(Document document)
    {
        var existing = await GetRequiredActiveDocumentAsync(document.Id);
        var username = GetCurrentUsername(document.UpdatedBy);

        var oldSnapshot = CloneForAudit(existing);

        existing.DocumentType = document.DocumentType;
        existing.DocumentNumber = document.DocumentNumber;
        existing.ReferenceNumber = document.ReferenceNumber;
        existing.Title = document.Title;
        existing.Summary = document.Summary;
        existing.ContentText = document.ContentText;
        existing.IssueDate = document.IssueDate;
        existing.ReceivedDate = document.ReceivedDate;
        existing.DueDate = document.DueDate;
        existing.SenderName = document.SenderName;
        existing.ReceiverName = document.ReceiverName;
        existing.SignerName = document.SignerName;
        existing.CategoryId = document.CategoryId;
        existing.StatusId = document.StatusId;
        existing.ConfidentialityLevel = document.ConfidentialityLevel;
        existing.UrgencyLevel = document.UrgencyLevel;
        existing.ProcessingDepartment = document.ProcessingDepartment;
        existing.AssignedTo = document.AssignedTo;
        existing.Notes = document.Notes;
        existing.IsExpired = document.IsExpired;
        existing.OcrStatus = document.OcrStatus;

        existing.SetUpdated(DateTime.UtcNow, username);

        var result = await _documentRepository.UpdateAsync(existing);

        if (!result)
        {
            return;
        }

        await AddHistoryAsync(
            existing.Id,
            "UPDATE",
            $"Cập nhật văn bản số: {existing.DocumentNumber}",
            username);

        await AddAuditAsync(
            existing.Id,
            "UPDATE",
            oldSnapshot,
            existing,
            username);
    }

    public async Task SoftDeleteAsync(long id)
    {
        var existing = await _documentRepository.GetByIdAsync(id);

        if (existing == null || !existing.IsActive)
        {
            return;
        }

        var username = GetCurrentUsername();
        var oldSnapshot = CloneForAudit(existing);

        var result = await _documentRepository.DeleteAsync(id);

        if (!result)
        {
            return;
        }

        existing.Deactivate(username);

        await AddHistoryAsync(
            id,
            "DELETE",
            $"Xóa văn bản số: {existing.DocumentNumber}",
            username);

        await AddAuditAsync(
            id,
            "DELETE",
            oldSnapshot,
            existing,
            username);
    }

    public async Task SubmitForApprovalAsync(long id)
    {
        var document = await GetRequiredActiveDocumentAsync(id);
        var username = GetCurrentUsername();

        var oldSnapshot = CloneForAudit(document);

        document.MarkAsIssued(username);

        var result = await _documentRepository.UpdateAsync(document);

        if (!result)
        {
            return;
        }

        await AddHistoryAsync(
            id,
            "ISSUE",
            $"Ghi nhận văn bản đã ban hành: {document.DocumentNumber}",
            username);

        await AddAuditAsync(
            id,
            "ISSUE",
            oldSnapshot,
            document,
            username);
    }

    public async Task ApproveAsync(long id)
    {
        var document = await GetRequiredActiveDocumentAsync(id);
        var username = GetCurrentUsername();

        var oldSnapshot = CloneForAudit(document);

        document.MarkAsIssued(username);

        var result = await _documentRepository.UpdateAsync(document);

        if (!result)
        {
            return;
        }

        await AddHistoryAsync(
            id,
            "ISSUE",
            $"Ghi nhận văn bản đã ban hành: {document.DocumentNumber}",
            username);

        await AddAuditAsync(
            id,
            "ISSUE",
            oldSnapshot,
            document,
            username);
    }

    public async Task RejectAsync(long id, string? reason)
    {
        var document = await GetRequiredActiveDocumentAsync(id);
        var username = GetCurrentUsername();

        var oldSnapshot = CloneForAudit(document);

        document.Notes = string.IsNullOrWhiteSpace(reason)
            ? document.Notes
            : reason;

        document.SetUpdated(DateTime.UtcNow, username);

        var result = await _documentRepository.UpdateAsync(document);

        if (!result)
        {
            return;
        }

        await AddHistoryAsync(
            id,
            "NOTE",
            $"Cập nhật ghi chú văn bản số: {document.DocumentNumber}",
            username);

        await AddAuditAsync(
            id,
            "NOTE",
            oldSnapshot,
            document,
            username);
    }

    public Task<List<CategoryModel>> GetCategoriesAsync()
    {
        return _documentRepository.GetCategoriesAsync();
    }

    public Task<List<StatusModel>> GetStatusesAsync()
    {
        return _documentRepository.GetStatusesAsync();
    }

    private async Task<Document> GetRequiredActiveDocumentAsync(long id)
    {
        var document = await GetByIdAsync(id);

        if (document == null)
        {
            throw new InvalidOperationException("Không tìm thấy văn bản hoặc văn bản đã bị xóa.");
        }

        return document;
    }

    private string GetCurrentUsername(string? fallback = null)
    {
        return _authService.CurrentUser?.Username
            ?? fallback
            ?? "system";
    }

    private Task AddHistoryAsync(
        long documentId,
        string action,
        string description,
        string username)
    {
        return _historyRepository.AddAsync(
            documentId,
            action,
            description,
            null,
            null,
            username);
    }

    private Task AddAuditAsync(
        long entityId,
        string action,
        Document? oldValue,
        Document? newValue,
        string username)
    {
        return _auditLogRepository.AddAsync(new AuditLog
        {
            EntityName = EntityName,
            EntityId = entityId,
            Action = action,
            OldValues = oldValue == null ? null : SerializeForAudit(oldValue),
            NewValues = newValue == null ? null : SerializeForAudit(newValue),
            ChangedColumns = GetChangedColumns(oldValue, newValue),
            Username = username,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static Document CloneForAudit(Document source)
    {
        return new Document
        {
            Id = source.Id,
            DocumentType = source.DocumentType,
            DocumentNumber = source.DocumentNumber,
            ReferenceNumber = source.ReferenceNumber,
            Title = source.Title,
            Summary = source.Summary,
            ContentText = source.ContentText,
            IssueDate = source.IssueDate,
            ReceivedDate = source.ReceivedDate,
            DueDate = source.DueDate,
            SenderName = source.SenderName,
            ReceiverName = source.ReceiverName,
            SignerName = source.SignerName,
            CategoryId = source.CategoryId,
            StatusId = source.StatusId,
            ConfidentialityLevel = source.ConfidentialityLevel,
            UrgencyLevel = source.UrgencyLevel,
            ProcessingDepartment = source.ProcessingDepartment,
            AssignedTo = source.AssignedTo,
            Notes = source.Notes,
            IsActive = source.IsActive,
            IsExpired = source.IsExpired,
            OcrStatus = source.OcrStatus,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
            CreatedBy = source.CreatedBy,
            UpdatedBy = source.UpdatedBy
        };
    }

    private static string? GetChangedColumns(Document? oldValue, Document? newValue)
    {
        if (oldValue == null && newValue == null)
        {
            return null;
        }

        if (oldValue == null)
        {
            return "CREATED";
        }

        if (newValue == null)
        {
            return "DELETED";
        }

        var changedColumns = new List<string>();

        AddIfChanged(changedColumns, nameof(Document.DocumentType), oldValue.DocumentType, newValue.DocumentType);
        AddIfChanged(changedColumns, nameof(Document.DocumentNumber), oldValue.DocumentNumber, newValue.DocumentNumber);
        AddIfChanged(changedColumns, nameof(Document.ReferenceNumber), oldValue.ReferenceNumber, newValue.ReferenceNumber);
        AddIfChanged(changedColumns, nameof(Document.Title), oldValue.Title, newValue.Title);
        AddIfChanged(changedColumns, nameof(Document.Summary), oldValue.Summary, newValue.Summary);
        AddIfChanged(changedColumns, nameof(Document.ContentText), oldValue.ContentText, newValue.ContentText);
        AddIfChanged(changedColumns, nameof(Document.IssueDate), oldValue.IssueDate, newValue.IssueDate);
        AddIfChanged(changedColumns, nameof(Document.ReceivedDate), oldValue.ReceivedDate, newValue.ReceivedDate);
        AddIfChanged(changedColumns, nameof(Document.DueDate), oldValue.DueDate, newValue.DueDate);
        AddIfChanged(changedColumns, nameof(Document.SenderName), oldValue.SenderName, newValue.SenderName);
        AddIfChanged(changedColumns, nameof(Document.ReceiverName), oldValue.ReceiverName, newValue.ReceiverName);
        AddIfChanged(changedColumns, nameof(Document.SignerName), oldValue.SignerName, newValue.SignerName);
        AddIfChanged(changedColumns, nameof(Document.CategoryId), oldValue.CategoryId, newValue.CategoryId);
        AddIfChanged(changedColumns, nameof(Document.StatusId), oldValue.StatusId, newValue.StatusId);
        AddIfChanged(changedColumns, nameof(Document.ConfidentialityLevel), oldValue.ConfidentialityLevel, newValue.ConfidentialityLevel);
        AddIfChanged(changedColumns, nameof(Document.UrgencyLevel), oldValue.UrgencyLevel, newValue.UrgencyLevel);
        AddIfChanged(changedColumns, nameof(Document.ProcessingDepartment), oldValue.ProcessingDepartment, newValue.ProcessingDepartment);
        AddIfChanged(changedColumns, nameof(Document.AssignedTo), oldValue.AssignedTo, newValue.AssignedTo);
        AddIfChanged(changedColumns, nameof(Document.Notes), oldValue.Notes, newValue.Notes);
        AddIfChanged(changedColumns, nameof(Document.IsActive), oldValue.IsActive, newValue.IsActive);
        AddIfChanged(changedColumns, nameof(Document.IsExpired), oldValue.IsExpired, newValue.IsExpired);
        AddIfChanged(changedColumns, nameof(Document.OcrStatus), oldValue.OcrStatus, newValue.OcrStatus);
        AddIfChanged(changedColumns, nameof(Document.CreatedAt), oldValue.CreatedAt, newValue.CreatedAt);
        AddIfChanged(changedColumns, nameof(Document.UpdatedAt), oldValue.UpdatedAt, newValue.UpdatedAt);
        AddIfChanged(changedColumns, nameof(Document.CreatedBy), oldValue.CreatedBy, newValue.CreatedBy);
        AddIfChanged(changedColumns, nameof(Document.UpdatedBy), oldValue.UpdatedBy, newValue.UpdatedBy);

        return changedColumns.Count == 0
            ? null
            : string.Join(",", changedColumns);
    }

    private static void AddIfChanged<T>(List<string> changedColumns, string columnName, T oldValue, T newValue)
    {
        if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            changedColumns.Add(columnName);
        }
    }

    private static string SerializeForAudit(Document document)
    {
        return JsonSerializer.Serialize(document, new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }
}
