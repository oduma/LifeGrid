namespace LifeGrid.Application.Week;

public static class WeekClosureStateComputer
{
    public static (bool IsLoggingEnabled, bool IsCloseWeekButtonVisible, bool IsSummaryButtonVisible)
        Compute(string status, DateTime startDate, DateTime now)
    {
        var isClosed = status == "Closed";
        var endDate  = startDate.AddDays(6);
        var isPast   = endDate.Date < now.Date;
        return (!isClosed, isPast && !isClosed, isClosed);
    }
}
