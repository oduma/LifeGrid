using LifeGrid.Application.UserProfile;
using LifeGrid.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using UserProfileAggregate = LifeGrid.Domain.UserProfile.UserProfile;

namespace LifeGrid.Infrastructure.Data.Repositories;

public sealed class UserProfileRepository(LifeGridDbContext db) : IUserProfileRepository
{
    public Task<UserProfileAggregate?> GetSingleAsync(CancellationToken ct = default)
        => db.UserProfiles.FirstOrDefaultAsync(ct);

    public async Task AddAsync(UserProfileAggregate profile, CancellationToken ct = default)
    {
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync(ct);
    }
}
