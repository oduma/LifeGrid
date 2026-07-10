namespace LifeGrid.Application.FlashQuest;

public interface IFlashQuestTriggerService
{
    Task EvaluateAsync(CancellationToken ct = default);
}
