using System.Globalization;
using DocumentManagement.Contracts.Documents;

namespace DocumentManagement.Wpf.ViewModels;

public class DocumentRowViewModel : BaseViewModel
{
    private bool _isBatchSelected;

    public DocumentRowViewModel(DocumentDto document)
    {
        Document = document;
    }

    public event EventHandler<bool>? BatchSelectionChanged;

    public DocumentDto Document { get; }

    public long Id => Document.Id;

    public string DocumentNumber => string.IsNullOrWhiteSpace(Document.DocumentNumber)
        ? "(chưa có số)"
        : Document.DocumentNumber;

    public string Title => Document.Title;

    public string DocumentType => Document.DocumentType;

    public string StatusText => Document.StatusText;

    public string StatusColor => Document.StatusColor;

    public string UrgencyText => Document.UrgencyText;

    public string UrgencyColor => Document.UrgencyColor;

    public string? SenderName => Document.SenderName;

    public string? ReceiverName => Document.ReceiverName;

    public string? ProcessingDepartment => Document.ProcessingDepartment;

    public string? AssignedTo => Document.AssignedTo;

    public string? Notes => Document.Notes;

    public string? IssueDate => Document.IssueDate;

    public string? DueDate => Document.DueDate;

    public DateTime UpdatedAt => Document.UpdatedAt;

    public bool IsArchived =>
        string.Equals(Document.StatusCode, "ARCHIVED", StringComparison.OrdinalIgnoreCase)
        || Document.StatusId == DocumentWorkflowStatus.Archived;

    public bool IsUrgent =>
        string.Equals(Document.UrgencyLevel, "URGENT", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Document.UrgencyLevel, "VERY_URGENT", StringComparison.OrdinalIgnoreCase);

    public bool IsUnread => UpdatedAt.Date >= DateTime.Today.AddDays(-1);

    public bool IsBlocked => DueDateValue.HasValue && DueDateValue.Value.Date < DateTime.Today && !IsArchived;

    public bool IsDueToday => DueDateValue.HasValue && DueDateValue.Value.Date == DateTime.Today && !IsArchived;

    public string SlaBrush => IsArchived
        ? "#8B5CF6"
        : IsBlocked
            ? "#DC2626"
            : IsDueToday
                ? "#F59E0B"
                : IsUrgent
                    ? "#0EA5E9"
                    : "#10B981";

    public string SlaText => IsArchived
        ? "Lưu trữ"
        : IsBlocked
            ? "Quá hạn"
            : IsDueToday
                ? "Hôm nay"
                : IsUrgent
                    ? "Ưu tiên"
                    : "Ổn định";

    public string UpdatedAtText => UpdatedAt == default
        ? string.Empty
        : UpdatedAt.ToString("dd/MM HH:mm", CultureInfo.CurrentCulture);

    public string PreviewMeta => $"{DocumentNumber} - {StatusText} - {SlaText}";

    public bool IsBatchSelected
    {
        get => _isBatchSelected;
        set
        {
            if (SetProperty(ref _isBatchSelected, value))
            {
                BatchSelectionChanged?.Invoke(this, value);
            }
        }
    }

    private DateTime? DueDateValue => ParseDate(Document.DueDate);

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string[] formats = ["yyyy-MM-dd", "dd/MM/yyyy", "M/d/yyyy", "MM/dd/yyyy"];

        if (DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed)
            ? parsed
            : null;
    }
}

public static class DocumentWorkflowStatus
{
    public const long Draft = 1;
    public const long Issued = 4;
    public const long Archived = 5;
}
