namespace LifeGrid.Presentation.ViewModels;

public sealed record HabitCompletionLogItem(
    Guid     LogId,
    double   ActualValue,
    string   MeasurementUnit,
    string?  ProofText,
    string?  ProofImageUrl,
    DateTime Timestamp)
{
    public bool    HasProofImage    => !string.IsNullOrWhiteSpace(ProofImageUrl);
    public bool    HasProofText     => !string.IsNullOrWhiteSpace(ProofText);
    public string  LogSummary       => $"{ActualValue} {MeasurementUnit} @ {Timestamp:HH:mm}";
}
