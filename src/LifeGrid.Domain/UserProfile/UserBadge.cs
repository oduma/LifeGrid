namespace LifeGrid.Domain.UserProfile;

public sealed class UserBadge
{
    private UserBadge() { }

    public Guid     BadgeId     { get; private set; }
    public string   BadgeType   { get; private set; } = string.Empty;
    public string   Description { get; private set; } = string.Empty;
    public DateTime DateEarned  { get; private set; }
}
