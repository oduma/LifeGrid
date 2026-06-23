using LifeGrid.Application.Common;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Habit;
using LifeGrid.Application.Onboarding;
using LifeGrid.Application.UserProfile;
using LifeGrid.Domain.Common;
using LifeGrid.Domain.Onboarding;
using MediatR;
using HabitEntity    = LifeGrid.Domain.Habit.Habit;
using WeekEntity     = LifeGrid.Domain.Week.Week;
using WeekGoalEntity = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Application.Week.Commands;

public record GenerateScheduleCommand : IRequest<Result<HabitGenerationOutcome>>;

public sealed class GenerateScheduleCommandHandler(
    IOnboardingRepository         onboardingRepository,
    IUserProfileRepository        userProfileRepository,
    IGoalRepository               goalRepository,
    IGeminiHabitGenerationService habitGenerationService,
    IWeekRepository               weekRepository,
    IHabitRepository              habitRepository,
    IUnitOfWork                   unitOfWork)
    : IRequestHandler<GenerateScheduleCommand, Result<HabitGenerationOutcome>>
{
    public async Task<Result<HabitGenerationOutcome>> Handle(
        GenerateScheduleCommand request,
        CancellationToken       cancellationToken)
    {
        var session = await onboardingRepository.GetActiveSessionAsync(cancellationToken);
        if (session is null)
            return Result<HabitGenerationOutcome>.Failure("No active onboarding session found.");

        if (session.CurrentStep != OnboardingStep.Step1_ExecutionVerified)
            return Result<HabitGenerationOutcome>.Failure(
                $"Session is not in the expected state. Current step: {session.CurrentStep}");

        if (session.GoalId is null)
            return Result<HabitGenerationOutcome>.Failure(
                "No goal linked to the current session. Please finalize the goal first.");

        var userProfile = await userProfileRepository.GetSingleAsync(cancellationToken);
        if (userProfile is null)
            return Result<HabitGenerationOutcome>.Failure("User profile not found.");

        var goal = await goalRepository.GetByIdAsync(session.GoalId.Value, cancellationToken);
        if (goal is null)
            return Result<HabitGenerationOutcome>.Failure("No goal found for the current session.");

        if (session.ChosenStartDate is null)
            return Result<HabitGenerationOutcome>.Failure("No chosen start date found in session.");

        if (session.BlueprintJson is null)
            return Result<HabitGenerationOutcome>.Failure(
                "No blueprint cached for this goal. Run GenerateBlueprintCommand first.");

        var activeGoalCount = await goalRepository.GetActiveCountAsync(userProfile.UserId, cancellationToken);
        var isFirstGoal     = activeGoalCount == 1;

        var serviceResult = await habitGenerationService.GenerateScheduleFromBlueprintAsync(
            session.BlueprintJson,
            session.ChosenStartDate.Value,
            cancellationToken);

        if (!serviceResult.IsSuccess)
            return Result<HabitGenerationOutcome>.Failure(serviceResult.Error!);

        if (serviceResult.Value is HabitSchedulingResult.Infeasible infeasible)
            return Result<HabitGenerationOutcome>.Success(
                new HabitGenerationOutcome.Infeasible(
                    infeasible.RecalibrationReason,
                    infeasible.SuggestedDeadline,
                    infeasible.SuggestedAlternativeScope));

        var feasible = (HabitSchedulingResult.Feasible)serviceResult.Value!;

        int weekGoalNumber = 0;
        foreach (var weekDto in feasible.Schedule)
        {
            weekGoalNumber++;

            var existingWeek = await weekRepository.GetByStartDateAsync(weekDto.StartDate, cancellationToken);
            WeekGoalEntity weekGoal;

            if (existingWeek is null)
            {
                var newWeek = WeekEntity.Create(weekDto.WeekNumber, weekDto.StartDate);
                weekGoal    = WeekGoalEntity.Create(newWeek.WeekId, goal.GoalId, weekGoalNumber);
                await weekRepository.AddAsync(newWeek, weekGoal, cancellationToken);
            }
            else
            {
                weekGoal = WeekGoalEntity.Create(existingWeek.WeekId, goal.GoalId, weekGoalNumber);
                await weekRepository.AddWeekGoalAsync(weekGoal, cancellationToken);
            }

            var weekDeadline = weekDto.StartDate.AddDays(6);
            var habits = weekDto.Habits
                .Select(h => HabitEntity.Create(
                    weekGoal.WeekGoalId, Domain.Habit.HabitType.Planned,
                    h.Description, h.Description, h.Value, h.Unit, weekDeadline))
                .ToList();

            await habitRepository.AddRangeAsync(habits, cancellationToken);
        }

        if (isFirstGoal)
            userProfile.GrantBonusShield();

        await unitOfWork.CommitAsync(cancellationToken);

        await onboardingRepository.DeleteAsync(session, cancellationToken);

        return Result<HabitGenerationOutcome>.Success(new HabitGenerationOutcome.Complete());
    }
}
