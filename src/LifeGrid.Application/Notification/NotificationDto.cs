namespace LifeGrid.Application.Notification;

public record NotificationDto(
    Guid     Id,
    string   Title,
    string   Message,
    string   TypeLabel,
    string?  DeepLinkUrl,
    bool     IsRead,
    DateTime Timestamp);
