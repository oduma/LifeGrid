namespace LifeGrid.Application.Badge;

public record BadgeDto(Guid BadgeId, string BadgeType, string IconName, string Description, DateTime DateEarned);
