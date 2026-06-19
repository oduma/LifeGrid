namespace LifeGrid.Domain.Goal;

public sealed class LinkedBadHabit
{
    private LinkedBadHabit() { }

    public static LinkedBadHabit Create(string description, int dangerLevel) => new()
    {
        BadHabitId  = Guid.NewGuid(),
        Description = description,
        DangerLevel = dangerLevel
    };

    public Guid   BadHabitId  { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public int    DangerLevel { get; private set; }
}
