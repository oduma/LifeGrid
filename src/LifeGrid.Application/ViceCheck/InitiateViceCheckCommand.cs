using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.ViceCheck;

public record InitiateViceCheckCommand(Guid WeekId) : IRequest<Result<InitiateViceCheckResult>>;

public record InitiateViceCheckResult(Guid AuditId, string Question);
