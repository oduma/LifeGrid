using LifeGrid.Application.Gamification;
using LifeGrid.Application.Onboarding.Queries;
using LifeGrid.Infrastructure.Data;
using LifeGrid.Infrastructure.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using LifeGrid.Presentation.Pages;
using LifeGrid.Presentation.Services;
using LifeGrid.Presentation.ViewModels;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace LifeGrid.Presentation;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("DMMono-Regular.ttf",         "DMMono-Regular");
                fonts.AddFont("DMMono-Medium.ttf",          "DMMono-Medium");
                fonts.AddFont("DMMono-Italic.ttf",          "DMMono-Italic");
                fonts.AddFont("ShareTechMono-Regular.ttf",  "ShareTechMono-Regular");
                fonts.AddFont("MaterialSymbolsRounded.ttf", "MaterialSymbolsRounded");
            });

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "lifegrid.db");
        builder.Services.AddInfrastructure($"Data Source={dbPath}");

        builder.Services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(GetOrCreateOnboardingSessionQuery).Assembly));

        builder.Services.AddSingleton<IEconomyStateBroadcaster, WeakReferenceMessengerBroadcaster>();
        builder.Services.AddSingleton<AppShellViewModel>();
        builder.Services.AddSingleton<HudViewModel>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<CreateGoalViewModel>();
        builder.Services.AddTransient<CreateGoalPage>();
        builder.Services.AddTransient<GoalsViewModel>();
        builder.Services.AddTransient<GoalsPage>();
        builder.Services.AddTransient<UserSetupViewModel>();
        builder.Services.AddTransient<UserSetupPage>();
        builder.Services.AddTransient<ViceSurveyViewModel>();
        builder.Services.AddTransient<ViceSurveyPage>();
        builder.Services.AddTransient<OverwhelmedRecalculateViewModel>();
        builder.Services.AddTransient<OverwhelmedRecalculatePage>();
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<HabitLoggingViewModel>();
        builder.Services.AddTransient<HabitLoggingPage>();
        builder.Services.AddTransient<TimelineViewModel>();
        builder.Services.AddTransient<TimelinePage>();
        builder.Services.AddTransient<WeeklyHabitsViewModel>();
        builder.Services.AddTransient<WeeklyHabitsPage>();
        builder.Services.AddTransient<VaultViewModel>();
        builder.Services.AddTransient<VaultPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LifeGridDbContext>();
            db.Database.Migrate();
        }

        return app;
    }
}
