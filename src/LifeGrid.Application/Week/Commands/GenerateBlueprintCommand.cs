using LifeGrid.Application.Goal;
using LifeGrid.Application.Onboarding;
using LifeGrid.Domain.Common;
using LifeGrid.Domain.Onboarding;
using MediatR;
using System.Text.Json;

namespace LifeGrid.Application.Week.Commands;

public record GenerateBlueprintCommand : IRequest<Result<HabitGenerationOutcome>>;

public sealed class GenerateBlueprintCommandHandler(
    IOnboardingRepository         onboardingRepository,
    IGoalRepository               goalRepository,
    IGeminiHabitGenerationService habitGenerationService)
    : IRequestHandler<GenerateBlueprintCommand, Result<HabitGenerationOutcome>>
{
    public async Task<Result<HabitGenerationOutcome>> Handle(
        GenerateBlueprintCommand request,
        CancellationToken        cancellationToken)
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

        if (session.ChosenStartDate is null)
            return Result<HabitGenerationOutcome>.Failure("No chosen start date found in session.");

        // Cache hit: blueprint already exists for this session's goal
        if (session.BlueprintJson is not null)
            return Result<HabitGenerationOutcome>.Success(new HabitGenerationOutcome.Complete());

        var goal = await goalRepository.GetByIdAsync(session.GoalId.Value, cancellationToken);
        if (goal is null)
            return Result<HabitGenerationOutcome>.Failure("No goal found for the current session.");

        // Cache miss: call prompt 2.1
        var blueprintResult = await habitGenerationService.GenerateBlueprintAsync(
            goal.Description,
            goal.DeadlineDate.ToString("yyyy-MM-dd"),
            BuildBaselineAnswersJson(goal),
            session.ChosenStartDate.Value,
            cancellationToken);

        if (!blueprintResult.IsSuccess)
            return Result<HabitGenerationOutcome>.Failure(blueprintResult.Error!);

        if (blueprintResult.Value is BlueprintResult.Infeasible infeasible)
            return Result<HabitGenerationOutcome>.Success(
                new HabitGenerationOutcome.Infeasible(
                    infeasible.Reason,
                    infeasible.SuggestedDeadline,
                    infeasible.SuggestedScope));

        var feasible = (BlueprintResult.Feasible)blueprintResult.Value!;
        session.CacheBlueprint(feasible.BlueprintJson);
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
