using LifeGrid.Application.Common;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Habit;
using LifeGrid.Application.Onboarding;
using LifeGrid.Application.UserProfile;
using LifeGrid.Application.Vice;
using LifeGrid.Application.Week;
using LifeGrid.Infrastructure.AI;
using LifeGrid.Infrastructure.Data;
using LifeGrid.Infrastructure.Data.Repositories;
using LifeGrid.Infrastructure.Data.Services;
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
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
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

        services.AddSingleton<HttpClient>(_ =>
        {
            var handler = new System.Net.Http.SocketsHttpHandler
            {
                KeepAlivePingPolicy  = System.Net.Http.HttpKeepAlivePingPolicy.WithActiveRequests,
                KeepAlivePingDelay   = TimeSpan.FromSeconds(20),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(15),
            };
            return new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
        });
        services.AddTransient<IChatClient, GeminiHttpChatClient>();
        services.AddTransient<IGeminiGoalValidationService, GeminiGoalValidationService>();
        services.AddTransient<IGeminiHabitGenerationService, GeminiHabitGenerationService>();
        services.AddTransient<IGeminiViceSurveyService, GeminiViceSurveyService>();
        services.AddTransient<IFactoryResetService, FactoryResetService>();

        return services;
    }
}
