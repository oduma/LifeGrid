using LifeGrid.Application.WeeklyHabits;
using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.Home;

public record GetCurrentWeekHabitsQuery : IRequest<Result<WeeklyHabitsDashboardDto>>;
