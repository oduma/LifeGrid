using ViceCheckAuditEntity = LifeGrid.Domain.ViceCheck.ViceCheckAudit;

namespace LifeGrid.Application.ViceCheck;

public interface IViceCheckAuditRepository
{
    Task AddAsync(ViceCheckAuditEntity audit, CancellationToken ct = default);
    Task<ViceCheckAuditEntity?> GetByIdAsync(Guid auditId, CancellationToken ct = default);
    Task<bool> HasAuditForWeekAsync(Guid weekId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetPreviousQuestionsForBadHabitAsync(Guid badHabitId, CancellationToken ct = default);
}
