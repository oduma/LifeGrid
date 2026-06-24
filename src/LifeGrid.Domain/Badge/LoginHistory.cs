namespace LifeGrid.Domain.Badge;

public sealed class LoginHistory
{
    private LoginHistory() { }

    public Guid     Id        { get; private set; }
    public Guid     UserId    { get; private set; }
    public DateTime Timestamp { get; private set; }

    public static LoginHistory Create(Guid userId, DateTime timestamp)
        => new() { Id = Guid.NewGuid(), UserId = userId, Timestamp = timestamp };
}
