using LifeGrid.Presentation.ViewModels;

namespace LifeGrid.Presentation.Pages;

public partial class NotificationInboxPage : ContentPage
{
    private readonly NotificationInboxViewModel _viewModel;

    public NotificationInboxPage(NotificationInboxViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await _viewModel.LoadAsync();
    }
}
