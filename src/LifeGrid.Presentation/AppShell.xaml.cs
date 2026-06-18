using LifeGrid.Presentation.Pages;
using LifeGrid.Presentation.ViewModels;

namespace LifeGrid.Presentation;

public partial class AppShell : Shell
{
    public AppShell(AppShellViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        Routing.RegisterRoute("create-goal", typeof(CreateGoalPage));
        Routing.RegisterRoute("user-setup",  typeof(UserSetupPage));
    }
}
