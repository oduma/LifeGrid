namespace LifeGrid.Application.Week;

public record WeekScheduleDto(int WeekNumber, DateTime StartDate, IReadOnlyList<HabitScheduleItemDto> Habits);
