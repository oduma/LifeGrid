using LifeGrid.Domain.Gamification;

namespace LifeGrid.Domain.UserProfile;

public sealed class UserProfile
{
    private UserProfile() { }

    public static UserProfile Create() => new()
    {
        UserId       = Guid.NewGuid(),
        CurrentLevel = 1,
        Economy      = UserEconomy.CreateDefault(),
        ActiveStates = UserActiveStates.CreateDefault()
    };

    public Guid             UserId                { get; private set; }
    public int              CurrentLevel          { get; private set; }
    public UserEconomy      Economy               { get; private set; } = null!;
    public UserActiveStates ActiveStates          { get; private set; } = null!;
    public bool             IsViceSurveyCompleted { get; private set; }

    public void GrantXp(int amount)  => Economy.GrantXp(amount);
    public void DeductXp(int amount) => Economy.DeductXp(amount);

    public void GrantSp(int amount)  => Economy.GrantSp(amount);
    public void DeductSp(int amount) => Economy.DeductSp(amount);

    public void ApplyXpAndLevelProgression(int xpEarned)
    {
        Economy.GrantXp(xpEarned);
        CurrentLevel = GamificationCalculationEngine.CalculateLevel(Economy.LifetimeXp);
    }

    public void UpdateLifetimeGpAverage(double average) => Economy.SetLifetimeGpAverage(average);

    public void GrantBonusShield() => Economy.GrantShield();

    public void GrantSurveyBonusShield()
    {
        if (IsViceSurveyCompleted) return;
        Economy.GrantSurveyBonusShield();
        IsViceSurveyCompleted = true;
    }

    public bool ConsumeShield() => Economy.ConsumeShield();

}
