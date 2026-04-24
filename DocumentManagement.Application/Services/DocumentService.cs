using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using DocumentManagement.Domain.Entities;

namespace DocumentManagement.Application.Services;

public class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IHistoryRepository _historyRepository;
    private readonly IAuthService _authService;

    public DocumentService(
        IDocumentRepository documentRepository,
        IHistoryRepository historyRepository,
        IAuthService authService)
    {
        _documentRepository = documentRepository;
        _historyRepository = historyRepository;
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

    public Task<PagedResult<Document>> SearchPagedAsync(DocumentSearchRequest request)
    {
        return _documentRepository.SearchPagedAsync(request);
    }

    public Task<Document?> GetByIdAsync(long id)
    {
        return _documentRepository.GetByIdAsync(id);
    }

    public async Task<long> CreateAsync(Document document)
    {
        var username = GetCurrentUsername();

        document.SetCreated(DateTime.UtcNow, username);
        document.IsActive = true;

        var id = await _documentRepository.CreateAsync(document);

        await AddHistoryAsync(
            id,
            "CREATE",
            $"Tạo mới văn bản số: {document.DocumentNumber}",
            username);

        return id;
    }

    public async Task UpdateAsync(Document document)
    {
        var username = GetCurrentUsername(document.UpdatedBy);

        document.SetUpdated(DateTime.UtcNow, username);

        var result = await _documentRepository.UpdateAsync(document);

        if (!result)
            return;

        await AddHistoryAsync(
            document.Id,
            "UPDATE",
            $"Cập nhật văn bản số: {document.DocumentNumber}",
            username);
    }

    public async Task SoftDeleteAsync(long id)
    {
        var existing = await GetRequiredDocumentAsync(id);
        var username = GetCurrentUsername();

        existing.Deactivate(username);

        var result = await _documentRepository.UpdateAsync(existing);

        if (!result)
            return;

        await AddHistoryAsync(
            id,
            "DELETE",
            $"Xóa văn bản số: {existing.DocumentNumber}",
            username);
    }

    public async Task SubmitForApprovalAsync(long id)
    {
        var document = await GetRequiredDocumentAsync(id);
        var username = GetCurrentUsername();

        document.SubmitForApproval(username);

        var result = await _documentRepository.UpdateAsync(document);

        if (!result)
            return;

        await AddHistoryAsync(
            id,
            "SUBMIT_FOR_APPROVAL",
            $"Gửi duyệt văn bản số: {document.DocumentNumber}",
            username);
    }

    public async Task ApproveAsync(long id)
    {
        var document = await GetRequiredDocumentAsync(id);
        var username = GetCurrentUsername();

        document.Approve(username);

        var result = await _documentRepository.UpdateAsync(document);

        if (!result)
            return;

        await AddHistoryAsync(
            id,
            "APPROVE",
            $"Phê duyệt văn bản số: {document.DocumentNumber}",
            username);
    }

    public async Task RejectAsync(long id, string? reason)
    {
        var document = await GetRequiredDocumentAsync(id);
        var username = GetCurrentUsername();

        document.Reject(username, reason);

        var result = await _documentRepository.UpdateAsync(document);

        if (!result)
            return;

        var message = string.IsNullOrWhiteSpace(reason)
            ? $"Từ chối văn bản số: {document.DocumentNumber}"
            : $"Từ chối văn bản số: {document.DocumentNumber}. Lý do: {reason}";

        await AddHistoryAsync(
            id,
            "REJECT",
            message,
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

    private async Task<Document> GetRequiredDocumentAsync(long id)
    {
        var document = await _documentRepository.GetByIdAsync(id);

        if (document == null)
            throw new InvalidOperationException("Không tìm thấy văn bản.");

        if (!document.IsActive)
            throw new InvalidOperationException("Văn bản đã bị xóa.");

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
}