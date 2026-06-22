using LifeGrid.Domain.Onboarding;

namespace LifeGrid.Application.Onboarding;

public interface IOnboardingRepository
{
    Task<OnboardingSession?> GetActiveSessionAsync(CancellationToken ct = default);
    Task<OnboardingSession>  UpsertAsync(OnboardingSession session, CancellationToken ct = default);
    Task                     DeleteAsync(OnboardingSession session, CancellationToken ct = default);
}
