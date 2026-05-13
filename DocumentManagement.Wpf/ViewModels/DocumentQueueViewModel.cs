namespace DocumentManagement.Wpf.ViewModels;

public class DocumentQueueViewModel : BaseViewModel
{
    public DocumentQueueViewModel(
        string code,
        string title,
        string description,
        long? statusId = null,
        string? urgency = null)
    {
        Code = code;
        Title = title;
        Description = description;
        StatusId = statusId;
        Urgency = urgency;
    }

    public string Code { get; }

    public string Title { get; }

    public string Description { get; }

    public long? StatusId { get; }

    public string? Urgency { get; }
}
