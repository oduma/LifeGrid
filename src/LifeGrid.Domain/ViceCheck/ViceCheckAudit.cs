namespace LifeGrid.Domain.ViceCheck;

public sealed class ViceCheckAudit
{
    private ViceCheckAudit() { }

    public static ViceCheckAudit Create(
        Guid     weekId,
        Guid     weekGoalId,
        Guid     badHabitId,
        string   goalDescription,
        string   badHabitDescription,
        int      dangerLevel,
        string   question,
        DateTime createdAt) => new()
    {
        AuditId             = Guid.NewGuid(),
        WeekId              = weekId,
        WeekGoalId          = weekGoalId,
        BadHabitId          = badHabitId,
        GoalDescription     = goalDescription,
        BadHabitDescription = badHabitDescription,
        DangerLevel         = dangerLevel,
        Question            = question,
        Status              = ViceCheckStatus.Pending,
        CreatedAt           = createdAt
    };

    public Guid           AuditId               { get; private set; }
    public Guid           WeekId                { get; private set; }
    public Guid           WeekGoalId            { get; private set; }
    public Guid           BadHabitId            { get; private set; }
    public string         GoalDescription       { get; private set; } = string.Empty;
    public string         BadHabitDescription   { get; private set; } = string.Empty;
    public int            DangerLevel           { get; private set; }
    public string         Question              { get; private set; } = string.Empty;
    public string?        Answer                { get; private set; }
    public ViceCheckStatus Status                { get; private set; }
    public double?         PenaltyPercentApplied { get; private set; }
    public DateTime        CreatedAt             { get; private set; }
    public DateTime?       ResolvedAt            { get; private set; }

    public void MarkPassed(string answer, DateTime resolvedAt)
    {
        Answer     = answer;
        Status     = ViceCheckStatus.Passed;
        ResolvedAt = resolvedAt;
    }

    public void MarkFailed(string answer, double penaltyPercent, DateTime resolvedAt)
    {
        Answer                = answer;
        Status                = ViceCheckStatus.Failed;
        PenaltyPercentApplied = penaltyPercent;
        ResolvedAt            = resolvedAt;
    }
}
