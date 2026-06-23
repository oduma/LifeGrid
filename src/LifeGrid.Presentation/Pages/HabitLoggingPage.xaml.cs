using LifeGrid.Presentation.ViewModels;

namespace LifeGrid.Presentation.Pages;

public partial class HabitLoggingPage : ContentPage
{
    public HabitLoggingPage(HabitLoggingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
