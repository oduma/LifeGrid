namespace LifeGrid.Domain.Badge;

public sealed class Badge
{
    private Badge() { }

    public Guid      BadgeId     { get; private set; }
    public Guid      UserId      { get; private set; }
    public Guid?     GoalId      { get; private set; }
    public Guid?     WeekId      { get; private set; }
    public string    BadgeType   { get; private set; } = string.Empty;
    public string    BadgeName   { get; private set; } = string.Empty;
    public string    Description { get; private set; } = string.Empty;
    public string    IconName    { get; private set; } = string.Empty;
    public BadgeTier Tier        { get; private set; }
    public bool      IsEarned    { get; private set; }
    public DateTime? DateEarned  { get; private set; }

    public static Badge CreateEarned(
        Guid userId, string badgeType, string badgeName,
        string description, string iconName, BadgeTier tier, DateTime dateEarned)
        => new()
        {
            BadgeId     = Guid.NewGuid(),
            UserId      = userId,
            BadgeType   = badgeType,
            BadgeName   = badgeName,
            Description = description,
            IconName    = iconName,
            Tier        = tier,
            IsEarned    = true,
            DateEarned  = dateEarned
        };
}
