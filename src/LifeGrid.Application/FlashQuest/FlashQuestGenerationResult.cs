namespace LifeGrid.Application.FlashQuest;

public abstract record FlashQuestGenerationResult
{
    public sealed record NotEligible : FlashQuestGenerationResult;

    public sealed record Accepted(IReadOnlyList<FlashQuestItem> Quests) : FlashQuestGenerationResult;
}

public record FlashQuestItem(
    Guid   SourceHabitId,
    string QuestName,
    string Description,
    double MeasureValue,
    string MeasureUnit);
