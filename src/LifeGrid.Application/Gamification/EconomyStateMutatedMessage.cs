namespace LifeGrid.Application.Gamification;

// CurrentSp/ShieldsAvailable carry post-mutation values for SP-changing broadcasts.
// Non-SP broadcasts (e.g., Hibernate) use the default (0, 0); subscribers reload via LoadAsync regardless.
public record EconomyStateMutatedMessage(int CurrentSp = 0, int ShieldsAvailable = 0);
