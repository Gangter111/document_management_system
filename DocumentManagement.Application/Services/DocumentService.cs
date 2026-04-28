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
            CreatedBy = source.CreatedBy,
            UpdatedBy = source.UpdatedBy
        };
    }

    private static string SerializeForAudit(Document document)
    {
        return JsonSerializer.Serialize(document, new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }
}