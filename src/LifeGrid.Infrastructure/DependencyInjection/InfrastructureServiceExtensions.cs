using LifeGrid.Application.Onboarding;
using LifeGrid.Infrastructure.Data;
using LifeGrid.Infrastructure.Data.Repositories;
using LifeGrid.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddSingleton<ISecureStorageService, SecureStorageService>();
        services.AddSingleton<IBuildSecretProvider, BuildSecretProvider>();
        services.AddTransient<IApiCredentialSyncService, ApiCredentialSyncService>();

        return services;
    }
}
