using LifeGrid.Application.Onboarding;
using LifeGrid.Domain.Onboarding;
using Microsoft.EntityFrameworkCore;

namespace LifeGrid.Infrastructure.Data.Repositories;

public sealed class OnboardingRepository(LifeGridDbContext db) : IOnboardingRepository
{
    public Task<OnboardingSession?> GetActiveSessionAsync(CancellationToken ct = default)
        => db.OnboardingSessions.FirstOrDefaultAsync(ct);

    public async Task<OnboardingSession> UpsertAsync(OnboardingSession session, CancellationToken ct = default)
    {
        var existing = await db.OnboardingSessions
            .FindAsync([session.SessionId], ct);

        if (existing is null)
            db.OnboardingSessions.Add(session);
        else
            db.Entry(existing).CurrentValues.SetValues(session);

        await db.SaveChangesAsync(ct);
        return session;
    }

    public async Task DeleteAsync(OnboardingSession session, CancellationToken ct = default)
    {
        db.OnboardingSessions.Remove(session);
        await db.SaveChangesAsync(ct);
    }
}
