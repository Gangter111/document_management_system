using DocumentManagement.Contracts.Documents;
using DocumentManagement.Wpf.ViewModels;

namespace DocumentManagement.Wpf.Services;

public sealed class DocumentWorkflowCommandService
{
    private readonly ApiService _apiService;

    public DocumentWorkflowCommandService(ApiService apiService)
    {
        _apiService = apiService;
    }

    public Task ArchiveAsync(DocumentRowViewModel row, CancellationToken cancellationToken = default)
    {
        return ChangeStatusAsync(row.Document, DocumentWorkflowStatus.Archived, cancellationToken);
    }

    public Task RestoreAsync(DocumentRowViewModel row, CancellationToken cancellationToken = default)
    {
        return ChangeStatusAsync(row.Document, DocumentWorkflowStatus.Issued, cancellationToken);
    }

    public async Task ChangeStatusAsync(DocumentDto document, long statusId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _apiService.UpdateDocumentAsync(document.Id, BuildUpdateRequest(document, statusId), cancellationToken);
    }

    private static UpdateDocumentRequest BuildUpdateRequest(DocumentDto document, long statusId)
    {
        return new UpdateDocumentRequest
        {
            Id = document.Id,
            DocumentType = document.DocumentType,
            DocumentNumber = document.DocumentNumber,
            ReferenceNumber = document.ReferenceNumber,
            Title = document.Title,
            Summary = document.Summary,
            ContentText = document.ContentText,
            IssueDate = document.IssueDate,
            ReceivedDate = document.ReceivedDate,
            DueDate = document.DueDate,
            SenderName = document.SenderName,
            ReceiverName = document.ReceiverName,
            SignerName = document.SignerName,
            CategoryId = document.CategoryId,
            StatusId = statusId,
            ConfidentialityLevel = document.ConfidentialityLevel,
            UrgencyLevel = document.UrgencyLevel,
            ProcessingDepartment = document.ProcessingDepartment,
            AssignedTo = document.AssignedTo,
            Notes = document.Notes,
            IsExpired = document.IsExpired,
            OcrStatus = document.OcrStatus,
            UpdatedBy = AuthSession.Username
        };
    }
}
