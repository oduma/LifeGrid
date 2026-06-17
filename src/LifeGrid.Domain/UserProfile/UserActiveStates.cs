namespace LifeGrid.Domain.UserProfile;

public sealed class UserActiveStates
{
    private UserActiveStates() { }

    public static UserActiveStates CreateDefault() => new()
    {
        DoubleXpMode   = false,
        DoubleXpExpiry = DateTime.MinValue
    };

    public bool     DoubleXpMode   { get; private set; }
    public DateTime DoubleXpExpiry { get; private set; }
}
