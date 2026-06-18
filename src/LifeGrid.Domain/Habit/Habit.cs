namespace LifeGrid.Domain.Habit;

public sealed class Habit
{
    private Habit() { }

    public static Habit Create(
        Guid     weekGoalId,
        string   habitName,
        string   habitDescription,
        double   targetValue,
        string   measurementUnit,
        DateTime deadlineDateTime) => new()
    {
        HabitId          = Guid.NewGuid(),
        WeekGoalId       = weekGoalId,
        HabitType        = HabitType.Planned,
        HabitName        = habitName,
        HabitDescription = habitDescription,
        TargetValue      = targetValue,
        MeasurementUnit  = measurementUnit,
        DeadlineDateTime = deadlineDateTime
    };

    public Guid      HabitId          { get; private set; }
    public Guid      WeekGoalId       { get; private set; }
    public HabitType HabitType        { get; private set; }
    public string    HabitName        { get; private set; } = string.Empty;
    public string    HabitDescription { get; private set; } = string.Empty;
    public double    TargetValue      { get; private set; }
    public string    MeasurementUnit  { get; private set; } = string.Empty;
    public DateTime  DeadlineDateTime { get; private set; }
}
