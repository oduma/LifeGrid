using LifeGrid.Application.Goal;
using LifeGrid.Application.Onboarding;
using LifeGrid.Application.UserProfile;
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

        services.AddSingleton<ISecureStorageService, SecureStorageService>();
        services.AddSingleton<IBuildSecretProvider, BuildSecretProvider>();
        services.AddTransient<IApiCredentialSyncService, ApiCredentialSyncService>();

        services.AddSingleton<HttpClient>();
        services.AddTransient<IChatClient, GeminiHttpChatClient>();
        services.AddTransient<IGeminiGoalValidationService, GeminiGoalValidationService>();

        return services;
    }
}
