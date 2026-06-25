using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeGrid.Application.Common;
using LifeGrid.Application.Notification;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace LifeGrid.Presentation.ViewModels;

public partial class NotificationInboxViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INavigationService   _navigation;

    [ObservableProperty] private ObservableCollection<NotificationItemViewModel> _notifications = [];
    [ObservableProperty] private bool _isEmptyStateVisible;

    public NotificationInboxViewModel(
        IServiceScopeFactory scopeFactory,
        INavigationService   navigation)
    {
        _scopeFactory = scopeFactory;
        _navigation   = navigation;
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        using var scope    = _scopeFactory.CreateScope();
        var       mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var result = await mediator.Send(new GetNotificationsQuery(), ct);
        if (!result.IsSuccess) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Notifications.Clear();
            foreach (var n in result.Value!)
                Notifications.Add(new NotificationItemViewModel(n));
            IsEmptyStateVisible = Notifications.Count == 0;
        });
    }

    [RelayCommand]
    private async Task TapNotificationAsync(NotificationItemViewModel item)
    {
        using var scope    = _scopeFactory.CreateScope();
        var       mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(new MarkNotificationReadCommand(item.Id));
        MainThread.BeginInvokeOnMainThread(item.MarkRead);

        if (item.DeepLinkUrl is null) return;

        var route = NotificationRouteParser.ToShellRoute(item.DeepLinkUrl);
        if (route is not null)
            await _navigation.NavigateToAsync(route);
    }
}
