using LifeGrid.Application.Common;

namespace LifeGrid.Presentation.Services;

internal sealed class ShellNavigationService : INavigationService
{
    public Task NavigateToAsync(string route)
        => Shell.Current.GoToAsync(route);
}
