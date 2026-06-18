using LifeGrid.Domain.WeekGoal;

namespace LifeGrid.Domain.Week;

public sealed class Week
{
    private readonly List<WeekGoal.WeekGoal> _weekGoals = new();
    private Week() { }

    public static Week Create(int weekNumber, DateTime startDate) => new()
    {
        WeekId              = Guid.NewGuid(),
        WeekNumber          = weekNumber,
        StartDate           = startDate,
        Status              = WeekStatus.Active,
        TotalWeeklySpEarned = 0
    };

    public Guid       WeekId              { get; private set; }
    public int        WeekNumber          { get; private set; }
    public DateTime   StartDate           { get; private set; }
    public WeekStatus Status              { get; private set; }
    public int        TotalWeeklySpEarned { get; private set; }

    public IReadOnlyCollection<WeekGoal.WeekGoal> WeekGoals => _weekGoals.AsReadOnly();

    internal void AddWeekGoal(WeekGoal.WeekGoal weekGoal) => _weekGoals.Add(weekGoal);
}
