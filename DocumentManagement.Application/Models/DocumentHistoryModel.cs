namespace DocumentManagement.Application.Models;

public class DocumentHistoryModel
{
    public long Id { get; set; }

    public long? DocumentId { get; set; }

    public string ActionType { get; set; } = string.Empty;

    public string? ActionDescription { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public DateTime ActionAt { get; set; }

    public string? ActionBy { get; set; }

    public string DisplayText =>
        string.IsNullOrWhiteSpace(ActionDescription)
            ? ActionType
            : ActionDescription;
}