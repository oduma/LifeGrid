using LifeGrid.Application.Common;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Habit;
using LifeGrid.Application.Onboarding;
using LifeGrid.Application.UserProfile;
using LifeGrid.Application.Week;
using LifeGrid.Infrastructure.AI;
using LifeGrid.Infrastructure.Data;
using LifeGrid.Infrastructure.Data.Repositories;
using LifeGrid.Infrastructure.Security;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace LifeGrid.Infrastructure.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<LifeGridDbContext>(opts =>
            opts.UseSqlite(connectionString));
        services.AddScoped<IOnboardingRepository, OnboardingRepository>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IGoalRepository, GoalRepository>();
        services.AddScoped<IWeekRepository, WeekRepository>();
        services.AddScoped<IHabitRepository, HabitRepository>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<LifeGridDbContext>());

        services.AddSingleton<ISecureStorageService, SecureStorageService>();
        services.AddSingleton<IBuildSecretProvider, BuildSecretProvider>();
        services.AddTransient<IApiCredentialSyncService, ApiCredentialSyncService>();

        services.AddSingleton<HttpClient>();
        services.AddTransient<IChatClient, GeminiHttpChatClient>();
        services.AddTransient<IGeminiGoalValidationService, GeminiGoalValidationService>();
        services.AddTransient<IGeminiHabitGenerationService, GeminiHabitGenerationService>();

        return services;
    }
}
