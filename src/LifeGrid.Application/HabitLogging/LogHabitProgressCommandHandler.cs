using LifeGrid.Application.Common;
using LifeGrid.Application.Habit;
using LifeGrid.Domain.Common;
using LifeGrid.Domain.Habit;
using MediatR;

namespace LifeGrid.Application.HabitLogging;

public sealed class LogHabitProgressCommandHandler(
    IHabitRepository  habitRepository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork       unitOfWork)
    : IRequestHandler<LogHabitProgressCommand, Result>
{
    public async Task<Result> Handle(
        LogHabitProgressCommand request, CancellationToken cancellationToken)
    {
        if (request.ActualValue <= 0)
            return Result.Failure("Actual value must be greater than zero.");

        var habit = await habitRepository.GetByIdAsync(request.HabitId, cancellationToken);
        if (habit is null)
            return Result.Failure("Habit not found.");

        var log = CompletedValueLog.Create(
            request.HabitId,
            request.ActualValue,
            request.MeasurementUnit,
            request.ProofText,
            request.ProofImageUrl,
            dateTimeProvider.UtcNow);

        await habitRepository.AddCompletionLogAsync(log, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
