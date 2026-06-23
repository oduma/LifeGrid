namespace LifeGrid.Domain.UserProfile;

public sealed class UserEconomy
{
    private UserEconomy() { }

    public static UserEconomy CreateDefault() => new()
    {
        LifetimeGpAverage = 0.0,
        LifetimeXp        = 0,
        CurrentSp         = 0,
        ShieldsAvailable  = 0,
        MaxShieldCap      = 2
    };

    public double LifetimeGpAverage { get; private set; }
    public int    LifetimeXp        { get; private set; }
    public int    CurrentSp         { get; private set; }
    public int    ShieldsAvailable  { get; private set; }
    public int    MaxShieldCap      { get; private set; }

    internal void GrantXp(int amount)   => LifetimeXp += amount;
    internal void DeductXp(int amount)  => LifetimeXp = Math.Max(0, LifetimeXp - amount);

    internal void GrantShield()
    {
        if (ShieldsAvailable < MaxShieldCap)
            ShieldsAvailable++;
    }

    internal void GrantSurveyBonusShield()
    {
        MaxShieldCap     = 3;
        ShieldsAvailable++;
    }

    internal void GrantSp(int amount)
    {
        CurrentSp += amount;
        while (CurrentSp >= 30)
        {
            CurrentSp -= 30;
            GrantShield();
        }
    }

    internal void SetLifetimeGpAverage(double value) => LifetimeGpAverage = value;
}
