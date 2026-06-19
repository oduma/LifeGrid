namespace LifeGrid.Domain.UserProfile;

public sealed class UserProfile
{
    private readonly List<UserBadge> _badges = new();
    private UserProfile() { }

    public static UserProfile Create() => new()
    {
        UserId       = Guid.NewGuid(),
        CurrentLevel = 1,
        Economy      = UserEconomy.CreateDefault(),
        ActiveStates = UserActiveStates.CreateDefault()
    };

    public Guid                          UserId       { get; private set; }
    public int                           CurrentLevel { get; private set; }
    public UserEconomy                   Economy      { get; private set; } = null!;
    public UserActiveStates              ActiveStates { get; private set; } = null!;
    public IReadOnlyCollection<UserBadge> Badges      => _badges.AsReadOnly();

    public void GrantBonusShield() => Economy.GrantShield();
}
