using LifeGrid.Presentation.ViewModels;

namespace LifeGrid.Presentation.Pages;

public partial class OverwhelmedRecalculatePage : ContentPage
{
    public OverwhelmedRecalculatePage(OverwhelmedRecalculateViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
