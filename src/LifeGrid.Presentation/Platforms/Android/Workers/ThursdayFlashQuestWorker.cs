using Android.Content;
using AndroidX.Work;
using LifeGrid.Application.FlashQuest;
using Microsoft.Extensions.DependencyInjection;

namespace LifeGrid.Presentation.Workers;

[Android.Runtime.Register("com.lifegrid.app.ThursdayFlashQuestWorker")]
public sealed class ThursdayFlashQuestWorker : Worker
{
    public ThursdayFlashQuestWorker(Context context, WorkerParameters workerParams)
        : base(context, workerParams) { }

    public override Result DoWork()
    {
        try
        {
            using var scope = IPlatformApplication.Current!.Services.CreateScope();
            var triggerService = scope.ServiceProvider.GetRequiredService<IFlashQuestTriggerService>();
            Task.Run(async () => await triggerService.EvaluateAsync()).GetAwaiter().GetResult();
            return Result.InvokeSuccess();
        }
        catch
        {
            return Result.InvokeFailure();
        }
    }
}
