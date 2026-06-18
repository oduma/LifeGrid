using LifeGrid.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace LifeGrid.Infrastructure.Data.Services;

internal sealed class FactoryResetService(LifeGridDbContext db) : IFactoryResetService
{
    public async Task ResetAsync(CancellationToken ct = default)
    {
        // Delete all domain data in FK-safe order
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Habits",                ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM WeekGoals",             ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Weeks",                 ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM GoalRefinementAnswers", ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM GoalLinkedBadHabits",   ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Goals",                 ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM UserEconomy",           ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM UserActiveStates",      ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM UserBadges",            ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM UserProfiles",          ct);

        // Reset the onboarding session — query without IsComplete filter since
        // a completed session (IsComplete=true) is excluded by GetActiveSessionAsync
        var session = await db.OnboardingSessions.FirstOrDefaultAsync(ct);
        if (session is not null)
        {
            session.Reset();
            await db.SaveChangesAsync(ct);
        }
    }
}
