namespace LifeGrid.Application.Common;

public static class DateTimeExtensions
{
    public static DateTime ToUtcStart(this DateTime date)
        => DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

    public static DateTime ToUtcEndOfDay(this DateTime date)
        => DateTime.SpecifyKind(date.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
}
