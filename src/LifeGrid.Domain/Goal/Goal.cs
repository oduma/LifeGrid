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
        DateTime deadlineDate) => new()
    {
        GoalId       = Guid.NewGuid(),
        UserId       = userId,
        Description  = description,
        AmbientTag   = ambientTag,
        Duration     = duration,
        DeadlineDate = deadlineDate,
        Status       = GoalStatus.Active
    };

    public Guid       GoalId       { get; private set; }
    public Guid       UserId       { get; private set; }
    public string     Description  { get; private set; } = string.Empty;
    public string     AmbientTag   { get; private set; } = string.Empty;
    public string     Duration     { get; private set; } = string.Empty;
    public DateTime   DeadlineDate { get; private set; }
    public GoalStatus Status       { get; private set; }
    public IReadOnlyCollection<LinkedBadHabit>       LinkedBadHabits   => _linkedBadHabits.AsReadOnly();
    public IReadOnlyCollection<GoalRefinementAnswer> RefinementAnswers => _refinementAnswers.AsReadOnly();

    public void SetRefinementAnswers(IEnumerable<(int rankOrder, string question, string? answer)> items)
    {
        _refinementAnswers.Clear();
        foreach (var (rankOrder, question, answer) in items)
            _refinementAnswers.Add(GoalRefinementAnswer.Create(rankOrder, question, answer));
    }
}
