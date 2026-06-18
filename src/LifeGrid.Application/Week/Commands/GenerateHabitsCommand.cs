using LifeGrid.Application.Common;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Habit;
using LifeGrid.Application.Onboarding;
using LifeGrid.Application.UserProfile;
using LifeGrid.Domain.Common;
using LifeGrid.Domain.Onboarding;
using MediatR;
using System.Text.Json;
using HabitEntity    = LifeGrid.Domain.Habit.Habit;
using WeekEntity     = LifeGrid.Domain.Week.Week;
using WeekGoalEntity = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Application.Week.Commands;

public record GenerateHabitsCommand : IRequest<Result<HabitGenerationOutcome>>;

public sealed class GenerateHabitsCommandHandler(
    IOnboardingRepository         onboardingRepository,
    IUserProfileRepository        userProfileRepository,
    IGoalRepository               goalRepository,
    IGeminiHabitGenerationService habitGenerationService,
    IWeekRepository               weekRepository,
    IHabitRepository              habitRepository,
    IUnitOfWork                   unitOfWork)
    : IRequestHandler<GenerateHabitsCommand, Result<HabitGenerationOutcome>>
{
    public async Task<Result<HabitGenerationOutcome>> Handle(
        GenerateHabitsCommand request,
        CancellationToken     cancellationToken)
    {
        var session = await onboardingRepository.GetActiveSessionAsync(cancellationToken);
        if (session is null)
            return Result<HabitGenerationOutcome>.Failure("No active onboarding session found.");

        if (session.CurrentStep != OnboardingStep.Step1_ExecutionVerified)
            return Result<HabitGenerationOutcome>.Failure(
                $"Session is not in the expected state. Current step: {session.CurrentStep}");

        var userProfile = await userProfileRepository.GetSingleAsync(cancellationToken);
        if (userProfile is null)
            return Result<HabitGenerationOutcome>.Failure("User profile not found.");

        var goal = await goalRepository.GetByUserIdAsync(userProfile.UserId, cancellationToken);
        if (goal is null)
            return Result<HabitGenerationOutcome>.Failure("No goal found for the current user.");

        var serviceResult = await habitGenerationService.GenerateScheduleAsync(
            goal.Description,
            goal.DeadlineDate.ToString("yyyy-MM-dd"),
            BuildBaselineAnswersJson(goal),
            cancellationToken);

        if (!serviceResult.IsSuccess)
            return Result<HabitGenerationOutcome>.Failure(serviceResult.Error!);

        if (serviceResult.Value is HabitSchedulingResult.Infeasible infeasible)
        {
            return Result<HabitGenerationOutcome>.Success(
                new HabitGenerationOutcome.Infeasible(
                    infeasible.RecalibrationReason,
                    infeasible.SuggestedDeadline,
                    infeasible.SuggestedAlternativeScope));
        }

        var feasible = (HabitSchedulingResult.Feasible)serviceResult.Value!;

        foreach (var weekDto in feasible.Schedule)
        {
            var week     = WeekEntity.Create(weekDto.WeekNumber, weekDto.StartDate);
            var weekGoal = WeekGoalEntity.Create(week.WeekId, goal.GoalId);
            await weekRepository.AddAsync(week, weekGoal, cancellationToken);

            var weekDeadline = weekDto.StartDate.AddDays(6);
            var habits = weekDto.Habits
                .Select(h => HabitEntity.Create(
                    weekGoal.WeekGoalId, h.Description, h.Description,
                    h.Value, h.Unit, weekDeadline))
                .ToList();

            await habitRepository.AddRangeAsync(habits, cancellationToken);
        }

        await unitOfWork.CommitAsync(cancellationToken);

        session.AdvanceToHabitsGenerated();
        await onboardingRepository.UpsertAsync(session, cancellationToken);

        return Result<HabitGenerationOutcome>.Success(new HabitGenerationOutcome.Complete());
    }

    private static string BuildBaselineAnswersJson(Domain.Goal.Goal goal)
    {
        var answers = goal.RefinementAnswers
            .OrderBy(a => a.RankOrder)
            .Select(a => new { question = a.Question, answer = a.Answer ?? string.Empty })
            .ToList();

        return JsonSerializer.Serialize(answers);
    }
}
