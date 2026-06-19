namespace LifeGrid.Application.Hud;

public record HudTelemetryDto(
    int    Level,
    double LifetimeGp,
    double WeeklyGp,
    int    LifetimeXp,
    int    WeeklyXp,
    int    CurrentSp,
    int    WeeklySpEarned,
    int    ActiveShields,
    int    ShieldCap);
