using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.ViceCheck;

public record ResolveViceCheckCommand(Guid AuditId, string Answer) : IRequest<Result<ResolveViceCheckResult>>;

public record ResolveViceCheckResult(bool Persists, double? NewGp, double? PenaltyPercent, bool TriggersOverwhelmed);
