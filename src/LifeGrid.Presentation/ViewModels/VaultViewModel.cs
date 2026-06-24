using CommunityToolkit.Mvvm.ComponentModel;
using LifeGrid.Application.Badge;
using MediatR;
using System.Collections.ObjectModel;

namespace LifeGrid.Presentation.ViewModels;

public partial class VaultViewModel : ObservableObject
{
    private readonly IMediator _mediator;

    public VaultViewModel(IMediator mediator) { _mediator = mediator; }

    [ObservableProperty] private bool _isEmptyStateVisible;
    [ObservableProperty] private int  _gridSpan = 3;

    public ObservableCollection<VaultBadgeItem> Badges { get; } = new();

    public async Task LoadAsync()
    {
        var density = DeviceDisplay.Current.MainDisplayInfo.Density;
        var widthDp = density > 0
            ? DeviceDisplay.Current.MainDisplayInfo.Width / density
            : 360;
        GridSpan = widthDp >= 400 ? 4 : 3;

        var result = await _mediator.Send(new GetUserBadgesQuery());
        Badges.Clear();

        if (!result.IsSuccess || result.Value is null || !result.Value.Any())
        {
            IsEmptyStateVisible = true;
            return;
        }

        IsEmptyStateVisible = false;
        foreach (var dto in result.Value)
            Badges.Add(new VaultBadgeItem
            {
                IconGlyph   = dto.IconName,
                Title       = dto.BadgeType,
                Description = dto.Description,
                DateEarned  = dto.DateEarned
            });
    }
}
