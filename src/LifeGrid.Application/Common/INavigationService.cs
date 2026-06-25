namespace LifeGrid.Application.Common;

public interface INavigationService
{
    Task NavigateToAsync(string route);
}
