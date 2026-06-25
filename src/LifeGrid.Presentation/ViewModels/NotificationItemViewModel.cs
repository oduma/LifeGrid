using CommunityToolkit.Mvvm.ComponentModel;
using LifeGrid.Application.Notification;

namespace LifeGrid.Presentation.ViewModels;

public partial class NotificationItemViewModel : ObservableObject
{
    public Guid    Id          { get; }
    public string  Title       { get; }
    public string  Message     { get; }
    public string  TypeLabel   { get; }
    public string? DeepLinkUrl { get; }
    public string  FormattedTs { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUnread))]
    private bool _isRead;

    public bool IsUnread => !IsRead;

    public NotificationItemViewModel(NotificationDto dto)
    {
        Id          = dto.Id;
        Title       = dto.Title;
        Message     = dto.Message;
        TypeLabel   = dto.TypeLabel;
        DeepLinkUrl = dto.DeepLinkUrl;
        FormattedTs = dto.Timestamp.ToLocalTime().ToString("dd MMM, HH:mm");
        IsRead      = dto.IsRead;
    }

    public void MarkRead() => IsRead = true;
}
