namespace LifeGrid.Domain.Notification;

public sealed class Notification
{
    private Notification() { }

    public Guid             NotificationId { get; private set; }
    public string           Title          { get; private set; } = string.Empty;
    public string           Message        { get; private set; } = string.Empty;
    public NotificationType Type           { get; private set; }
    public string?          DeepLinkUrl    { get; private set; }
    public bool             IsRead         { get; private set; }
    public DateTime         Timestamp      { get; private set; }

    public static Notification Create(
        string title, string message, NotificationType type,
        string? deepLinkUrl, DateTime timestamp)
        => new()
        {
            NotificationId = Guid.NewGuid(),
            Title          = title,
            Message        = message,
            Type           = type,
            DeepLinkUrl    = deepLinkUrl,
            IsRead         = false,
            Timestamp      = timestamp
        };

    public void MarkRead() => IsRead = true;
}
