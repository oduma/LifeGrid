namespace LifeGrid.Application.ViceCheck;

public static class ViceCheckAvailabilityComputer
{
    public static bool IsVisible(
        bool     isViceSurveyCompleted,
        bool     isWeekClosed,
        DateTime weekStartDate,
        DateTime now,
        bool     alreadyAudited)
    {
        if (!isViceSurveyCompleted || !isWeekClosed || alreadyAudited) return false;
        var cutoff = weekStartDate.AddDays(6).AddHours(72);
        return now <= cutoff;
    }
}
