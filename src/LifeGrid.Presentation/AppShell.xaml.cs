using LifeGrid.Presentation.Pages;
using LifeGrid.Presentation.ViewModels;

namespace LifeGrid.Presentation;

public partial class AppShell : Shell
{
    public AppShell(AppShellViewModel viewModel, HudViewModel hudViewModel)
    {
        InitializeComponent();
        BindingContext            = viewModel;
        HudControl.BindingContext = hudViewModel;
        Loaded += async (_, _) => await hudViewModel.LoadAsync();
        Routing.RegisterRoute("create-goal",             typeof(CreateGoalPage));
        Routing.RegisterRoute("user-setup",              typeof(UserSetupPage));
        Routing.RegisterRoute("vice-survey",             typeof(ViceSurveyPage));
        Routing.RegisterRoute("overwhelmed-recalculate", typeof(OverwhelmedRecalculatePage));
        Routing.RegisterRoute("week-detail",             typeof(WeeklyHabitsPage));
        Routing.RegisterRoute("habit-logging",           typeof(HabitLoggingPage));
        Routing.RegisterRoute("notification-inbox",      typeof(NotificationInboxPage));
        Routing.RegisterRoute("week-summary",            typeof(WeekSummaryPage));

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(AppShellViewModel.IsProfileActive)) return;
            var res       = Microsoft.Maui.Controls.Application.Current!.Resources;
            var primary   = (Color)res["Primary"];
            var onSurface = (Color)res["OnSurface"];
            var tabActive = viewModel.IsProfileActive ? onSurface : primary;
            Shell.SetTabBarForegroundColor(this, tabActive);
            Shell.SetTabBarTitleColor(this, tabActive);
        };
    }
}
