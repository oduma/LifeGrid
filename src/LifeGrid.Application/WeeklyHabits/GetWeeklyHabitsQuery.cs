using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.WeeklyHabits;

public record GetWeeklyHabitsQuery(
    Guid                 WeekId,
    IReadOnlyList<Guid>? FilterGoalIds = null)
    : IRequest<Result<WeeklyHabitsDashboardDto>>;
