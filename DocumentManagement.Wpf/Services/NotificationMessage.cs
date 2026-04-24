namespace DocumentManagement.Wpf.Services;

public sealed class NotificationMessage
{
    public NotificationMessage(
        string title,
        string message,
        NotificationType type,
        TimeSpan duration)
    {
        Title = title;
        Message = message;
        Type = type;
        Duration = duration;
        CreatedAt = DateTime.Now;
    }

    public string Title { get; }

    public string Message { get; }

    public NotificationType Type { get; }

    public TimeSpan Duration { get; }

    public DateTime CreatedAt { get; }
}