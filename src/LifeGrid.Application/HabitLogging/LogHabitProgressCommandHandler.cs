using LifeGrid.Application.Common;
using LifeGrid.Application.Gamification;
using LifeGrid.Application.Habit;
using LifeGrid.Application.UserProfile;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Common;
using LifeGrid.Domain.Gamification;
using LifeGrid.Domain.Habit;
using MediatR;

namespace LifeGrid.Application.HabitLogging;

public sealed class LogHabitProgressCommandHandler(
    IHabitRepository         habitRepository,
    IWeekRepository          weekRepository,
    IUserProfileRepository   userProfileRepository,
    IDateTimeProvider        dateTimeProvider,
    IUnitOfWork              unitOfWork,
    IEconomyStateBroadcaster broadcaster)
    : IRequestHandler<LogHabitProgressCommand, Result>
{
    public async Task<Result> Handle(
        LogHabitProgressCommand request, CancellationToken cancellationToken)
    {
        if (request.ActualValue <= 0)
            return Result.Failure("Actual value must be greater than zero.");

        var habit = await habitRepository.GetByIdAsync(request.HabitId, cancellationToken);
        if (habit is null)
            return Result.Failure("Habit not found.");

        var weekGoal = await weekRepository.GetWeekGoalByIdAsync(habit.WeekGoalId, cancellationToken);
        if (weekGoal is null)
            return Result.Failure("WeekGoal not found.");

        var week = await weekRepository.GetByIdAsync(weekGoal.WeekId, cancellationToken);
        if (week is null)
            return Result.Failure("Week not found.");

        var profile = await userProfileRepository.GetSingleAsync(cancellationToken);
        if (profile is null)
            return Result.Failure("UserProfile not found.");

        var log = CompletedValueLog.Create(
            request.HabitId,
            request.ActualValue,
            request.MeasurementUnit,
            request.ProofText,
            request.ProofImageUrl,
            dateTimeProvider.UtcNow);

        await habitRepository.AddCompletionLogAsync(log, cancellationToken);

        // Load existing completion totals from DB (before the staged log is committed)
        var summaries = await habitRepository.GetCompletionSummariesForWeekGoalAsync(
            habit.WeekGoalId, cancellationToken);

        // Adjust the summary for the habit being logged to include the new entry
        var adjustedSummaries = summaries
            .Select(s => s.HabitId == request.HabitId
                ? s with { TotalActualValue = s.TotalActualValue + request.ActualValue }
                : s)
            .ToList();

        // Re-entry weeks lower the required target by 30% to ease the user back in
        double effectiveTarget = week.IsReEntryWeek
            ? Math.Ceiling(habit.TargetValue * 0.7)
            : habit.TargetValue;

        bool hasProof = request.ProofText is not null || request.ProofImageUrl is not null;
        var  reward   = GamificationCalculationEngine.CalculateEntryReward(
            habit.HabitType, request.ActualValue, effectiveTarget, hasProof);

        double newWeekGoalGp = GamificationCalculationEngine.CalculateWeekGoalGp(
            adjustedSummaries
                .Select(s => s.HabitId == request.HabitId
                    ? (s.TotalActualValue, effectiveTarget, s.HabitType)
                    : (s.TotalActualValue, s.TargetValue,   s.HabitType))
                .ToList());

        // Capture old GP before mutation for LifetimeGpAverage delta
        double oldGp = weekGoal.GoalWeeklyGp;
        var (gpSum, gpCount) = await weekRepository.GetWeekGoalGpStatsAsync(cancellationToken);

        double newLifetimeGpAvg = gpCount > 0
            ? (gpSum - oldGp + newWeekGoalGp) / gpCount
            : newWeekGoalGp;

        // Apply all mutations — EF change tracking persists these in one CommitAsync
        weekGoal.RecordMetricsUpdate(newWeekGoalGp, reward.XpEarned);
        week.AddSpEarned(reward.SpEarned);
        profile.GrantSp(reward.SpEarned);
        profile.ApplyXpAndLevelProgression(reward.XpEarned);
        profile.UpdateLifetimeGpAverage(newLifetimeGpAvg);

        await unitOfWork.CommitAsync(cancellationToken);

        broadcaster.Broadcast();

        return Result.Success();
    }
}
