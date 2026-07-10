using AndroidX.Work;
using Java.Util.Concurrent;
using LifeGrid.Presentation.Workers;

namespace LifeGrid.Presentation.Platform;

internal static class WeekLifecycleScheduler
{
    private const string MondayWorkName    = "lifegrid-monday-week-reminder";
    private const string WednesdayWorkName = "lifegrid-wednesday-auto-close";
    private const string ThursdayWorkName  = "lifegrid-thursday-flash-quest";

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

        var thursdayRequest = BuildWeeklyRequest<Workers.ThursdayFlashQuestWorker>(
            DayOfWeek.Thursday, targetHour: 12);
        workManager.EnqueueUniquePeriodicWork(
            ThursdayWorkName,
            ExistingPeriodicWorkPolicy.Keep!,
            thursdayRequest);
    }

    private static PeriodicWorkRequest BuildWeeklyRequest<TWorker>(DayOfWeek targetDay, int targetHour = 9)
        where TWorker : Worker
    {
        var initialDelay = ComputeInitialDelay(targetDay, targetHour);
        return (PeriodicWorkRequest)new PeriodicWorkRequest.Builder(
                Java.Lang.Class.FromType(typeof(TWorker)),
                7,
                TimeUnit.Days!)
            .SetInitialDelay((long)initialDelay.TotalMilliseconds, TimeUnit.Milliseconds!)
            .Build();
    }

    internal static TimeSpan ComputeInitialDelay(DayOfWeek targetDay, int targetHour = 9)
    {
        var now    = DateTime.Now;
        var target = now.Date;

        int daysAhead = ((int)targetDay - (int)now.DayOfWeek + 7) % 7;
        if (daysAhead == 0 && now.Hour >= targetHour)
            daysAhead = 7;

        target = target.AddDays(daysAhead).AddHours(targetHour);
        return target - now;
    }
}
