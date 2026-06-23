namespace LifeGrid.Domain.Habit;

public sealed class CompletedValueLog
{
    private CompletedValueLog() { }

    public static CompletedValueLog Create(
        Guid     habitId,
        double   actualValue,
        string   measurementUnit,
        string?  proofText,
        string?  proofImageUrl,
        DateTime timestamp) => new()
    {
        LogId           = Guid.NewGuid(),
        HabitId         = habitId,
        ActualValue     = actualValue,
        MeasurementUnit = measurementUnit,
        ProofText       = proofText,
        ProofImageUrl   = proofImageUrl,
        Timestamp       = timestamp
    };

    public Guid     LogId           { get; private set; }
    public Guid     HabitId         { get; private set; }
    public double   ActualValue     { get; private set; }
    public string   MeasurementUnit { get; private set; } = string.Empty;
    public string?  ProofText       { get; private set; }
    public string?  ProofImageUrl   { get; private set; }
    public DateTime Timestamp       { get; private set; }
}
