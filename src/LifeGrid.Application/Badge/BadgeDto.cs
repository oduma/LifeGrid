using LifeGrid.Domain.Badge;

namespace LifeGrid.Application.Badge;

public record BadgeDto(
    Guid      BadgeId,
    string    BadgeType,
    string    BadgeName,
    string    IconName,
    string    Description,
    BadgeTier Tier,
    bool      IsEarned,
    DateTime? DateEarned);
