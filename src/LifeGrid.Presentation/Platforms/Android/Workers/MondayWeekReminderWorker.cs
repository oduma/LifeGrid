using Android.Content;
using AndroidX.Work;
using LifeGrid.Application.Week;
using Microsoft.Extensions.DependencyInjection;

namespace LifeGrid.Presentation.Workers;

[Android.Runtime.Register("com.lifegrid.app.MondayWeekReminderWorker")]
public sealed class MondayWeekReminderWorker : Worker
{
    public MondayWeekReminderWorker(Context context, WorkerParameters workerParams)
        : base(context, workerParams) { }

    public override Result DoWork()
    {
        try
        {
            using var scope = IPlatformApplication.Current!.Services.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<IWeekLifecycleSyncService>();
            Task.Run(async () => await syncService.EvaluateAsync()).GetAwaiter().GetResult();
            return Result.InvokeSuccess();
        }
        catch
        {
            return Result.InvokeFailure();
        }
    }
}
