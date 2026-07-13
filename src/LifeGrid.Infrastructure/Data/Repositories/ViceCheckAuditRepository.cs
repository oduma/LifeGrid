using LifeGrid.Application.ViceCheck;
using Microsoft.EntityFrameworkCore;
using ViceCheckAuditEntity = LifeGrid.Domain.ViceCheck.ViceCheckAudit;

namespace LifeGrid.Infrastructure.Data.Repositories;

internal sealed class ViceCheckAuditRepository(LifeGridDbContext db) : IViceCheckAuditRepository
{
    public Task AddAsync(ViceCheckAuditEntity audit, CancellationToken ct = default)
    {
        db.ViceCheckAudits.Add(audit);
        return Task.CompletedTask;
    }

    public Task<ViceCheckAuditEntity?> GetByIdAsync(Guid auditId, CancellationToken ct = default)
        => db.ViceCheckAudits.FirstOrDefaultAsync(a => a.AuditId == auditId, ct);

    public Task<bool> HasAuditForWeekAsync(Guid weekId, CancellationToken ct = default)
        => db.ViceCheckAudits.AnyAsync(a => a.WeekId == weekId, ct);

    public async Task<IReadOnlyList<string>> GetPreviousQuestionsForBadHabitAsync(
        Guid badHabitId, CancellationToken ct = default)
        => await db.ViceCheckAudits
            .Where(a => a.BadHabitId == badHabitId)
            .Select(a => a.Question)
            .ToListAsync(ct);
}
