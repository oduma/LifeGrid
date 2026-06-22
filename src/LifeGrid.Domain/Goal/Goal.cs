namespace LifeGrid.Domain.Goal;

public sealed class Goal
{
    private readonly List<LinkedBadHabit>       _linkedBadHabits   = new();
    private readonly List<GoalRefinementAnswer> _refinementAnswers = new();
    private Goal() { }

    public static Goal Create(
        Guid userId,
        string description,
        string ambientTag,
        string duration,
        DateTime deadlineDate,
        DateTime startDate,
        DateTime creationDate) => new()
    {
        GoalId       = Guid.NewGuid(),
        UserId       = userId,
        Description  = description,
        AmbientTag   = ambientTag,
        Duration     = duration,
        DeadlineDate = deadlineDate,
        StartDate    = startDate,
        CreationDate = creationDate,
        Status       = GoalStatus.Active
    };

    public static DateTime CalculateStartDate(DateTime creationDate)
    {
        var day          = creationDate.Date;
        int daysToMonday = ((int)DayOfWeek.Monday - (int)day.DayOfWeek + 7) % 7;
        return day.AddDays(daysToMonday);
    }

    public Guid       GoalId       { get; private set; }
    public Guid       UserId       { get; private set; }
    public string     Description  { get; private set; } = string.Empty;
    public string     AmbientTag   { get; private set; } = string.Empty;
    public string     Duration     { get; private set; } = string.Empty;
    public DateTime   StartDate    { get; private set; }
    public DateTime   CreationDate { get; private set; }
    public DateTime   DeadlineDate { get; private set; }
    public GoalStatus Status       { get; private set; }
    public IReadOnlyCollection<LinkedBadHabit>       LinkedBadHabits   => _linkedBadHabits.AsReadOnly();
    public IReadOnlyCollection<GoalRefinementAnswer> RefinementAnswers => _refinementAnswers.AsReadOnly();

    public void MarkAbandoned() => Status = GoalStatus.Abandoned;

    public void ExtendDeadlineByPercent(double percent)
    {
        var extensionDays = (DeadlineDate - StartDate).TotalDays * (percent / 100.0);
        DeadlineDate      = DeadlineDate.AddDays(Math.Round(extensionDays));
        var totalDays     = (int)Math.Round((DeadlineDate - StartDate).TotalDays);
        Duration          = totalDays >= 30
            ? $"{(int)Math.Round(totalDays / 30.44)} months"
            : $"{(int)Math.Ceiling(totalDays / 7.0)} weeks";
    }

    public void SetRefinementAnswers(IEnumerable<(int rankOrder, string question, string? answer)> items)
    {
        _refinementAnswers.Clear();
        foreach (var (rankOrder, question, answer) in items)
            _refinementAnswers.Add(GoalRefinementAnswer.Create(rankOrder, question, answer));
    }

    public void SetLinkedBadHabits(IEnumerable<(string description, int dangerLevel)> items)
    {
        _linkedBadHabits.Clear();
        foreach (var (description, dangerLevel) in items)
            _linkedBadHabits.Add(LinkedBadHabit.Create(description, dangerLevel));
    }
}
