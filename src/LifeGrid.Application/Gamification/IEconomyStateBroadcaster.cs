namespace LifeGrid.Application.Gamification;

public interface IEconomyStateBroadcaster
{
    // For structural broadcasts that do not mutate SP or shields (e.g., Hibernate).
    void Broadcast();
    // For SP-mutating operations — carries post-commit economy snapshot.
    void BroadcastEconomy(int currentSp, int shieldsAvailable);
}
