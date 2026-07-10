using LifeGrid.Application.Common;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Week;
using MediatR;

namespace LifeGrid.Application.FlashQuest;

public sealed class FlashQuestTriggerService(
    IWeekRepository   weekRepository,
    ISender           sender,
    IDateTimeProvider dateTimeProvider)
    : IFlashQuestTriggerService
{
    public async Task EvaluateAsync(CancellationToken ct = default)
    {
        var today = dateTimeProvider.UtcNow.Date;
        if (today.DayOfWeek != DayOfWeek.Thursday)
            return;

        int daysFromMon   = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var currentMonday = today.AddDays(-daysFromMon);

        var week = await weekRepository.GetByStartDateAsync(currentMonday, ct);
        if (week is null || week.Status != WeekStatus.Active)
            return;

        await sender.Send(new GenerateFlashQuestCommand(week.WeekId), ct);
    }
}
