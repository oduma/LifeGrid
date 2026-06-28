using AndroidX.Work;
using Java.Util.Concurrent;
using LifeGrid.Presentation.Workers;

namespace LifeGrid.Presentation.Platform;

internal static class WeekLifecycleScheduler
{
    private const string MondayWorkName    = "lifegrid-monday-week-reminder";
    private const string WednesdayWorkName = "lifegrid-wednesday-auto-close";

    public static void Schedule()
    {
        var workManager = WorkManager.GetInstance(global::Android.App.Application.Context);

        var mondayRequest = BuildWeeklyRequest<Workers.MondayWeekReminderWorker>(
            DayOfWeek.Monday);
        workManager.EnqueueUniquePeriodicWork(
            MondayWorkName,
            ExistingPeriodicWorkPolicy.Keep!,
            mondayRequest);

        var wednesdayRequest = BuildWeeklyRequest<Workers.WednesdayAutoCloseWorker>(
            DayOfWeek.Wednesday);
        workManager.EnqueueUniquePeriodicWork(
            WednesdayWorkName,
            ExistingPeriodicWorkPolicy.Keep!,
            wednesdayRequest);
    }

    private static PeriodicWorkRequest BuildWeeklyRequest<TWorker>(DayOfWeek targetDay)
        where TWorker : Worker
    {
        var initialDelay = ComputeInitialDelay(targetDay);
        return (PeriodicWorkRequest)new PeriodicWorkRequest.Builder(
                Java.Lang.Class.FromType(typeof(TWorker)),
                7,
                TimeUnit.Days!)
            .SetInitialDelay((long)initialDelay.TotalMilliseconds, TimeUnit.Milliseconds!)
            .Build();
    }

    internal static TimeSpan ComputeInitialDelay(DayOfWeek targetDay)
    {
        var now    = DateTime.Now;
        var target = now.Date;

        int daysAhead = ((int)targetDay - (int)now.DayOfWeek + 7) % 7;
        if (daysAhead == 0 && now.Hour >= 9)
            daysAhead = 7;

        target = target.AddDays(daysAhead).AddHours(9);
        return target - now;
    }
}
