using System.Text.Json;
using System.Text.Json.Serialization;
using LifeGrid.Application.Common;
using LifeGrid.Application.Gamification;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Common;
using LifeGrid.Domain.Gamification;
using LifeGrid.Domain.ViceCheck;
using MediatR;

namespace LifeGrid.Application.ViceCheck;

public sealed class ResolveViceCheckCommandHandler(
    IViceCheckAuditRepository auditRepository,
    IWeekRepository          weekRepository,
    IGoalRepository          goalRepository,
    IGeminiViceCheckService  geminiViceCheckService,
    IDateTimeProvider        dateTimeProvider,
    IUnitOfWork              unitOfWork,
    IEconomyStateBroadcaster broadcaster)
    : IRequestHandler<ResolveViceCheckCommand, Result<ResolveViceCheckResult>>
{
    public async Task<Result<ResolveViceCheckResult>> Handle(
        ResolveViceCheckCommand request, CancellationToken cancellationToken)
    {
        var audit = await auditRepository.GetByIdAsync(request.AuditId, cancellationToken);
        if (audit is null)
            return Result<ResolveViceCheckResult>.Failure("audit_not_found");

        if (audit.Status != ViceCheckStatus.Pending)
            return Result<ResolveViceCheckResult>.Failure("already_resolved");

        var payload = new ResponsePayload(
            audit.GoalDescription, audit.BadHabitDescription, audit.DangerLevel, audit.Question, request.Answer);
        var payloadJson = JsonSerializer.Serialize(payload, JsonOpts);

        var aiResult = await geminiViceCheckService.EvaluateAnswerAsync(payloadJson, cancellationToken);
        if (!aiResult.IsSuccess)
            return Result<ResolveViceCheckResult>.Failure(aiResult.Error!);

        var now = dateTimeProvider.UtcNow;

        if (!aiResult.Value!.Persists)
        {
            audit.MarkPassed(request.Answer, now);
            await unitOfWork.CommitAsync(cancellationToken);
            return Result<ResolveViceCheckResult>.Success(new ResolveViceCheckResult(false, null, null, false));
        }

        var weekGoal = await weekRepository.GetWeekGoalByIdAsync(audit.WeekGoalId, cancellationToken);
        if (weekGoal is null)
            return Result<ResolveViceCheckResult>.Failure("weekgoal_not_found");

        var newGp = GamificationCalculationEngine.ApplyVicePenalty(weekGoal.GoalWeeklyGp, audit.DangerLevel);
        weekGoal.ApplyRetroactiveGpPenalty(newGp);

        var esc = ProcrastinationEscalationEngine.Evaluate(
            weekGoal.PenaltyState, newGp, weekGoal.GoalWeeklyXpEarned);
        weekGoal.SetPenaltyState(esc.NewPenaltyState);
        weekGoal.ApplyXpPenalty(esc.PenalizedXp);

        if (esc.TriggersOverwhelmed)
        {
            var goal = await goalRepository.GetByIdAsync(weekGoal.GoalId, cancellationToken);
            goal?.MarkOverwhelmed();
        }

        audit.MarkFailed(request.Answer, audit.DangerLevel, now);

        await unitOfWork.CommitAsync(cancellationToken);
        broadcaster.Broadcast();

        return Result<ResolveViceCheckResult>.Success(
            new ResolveViceCheckResult(true, newGp, audit.DangerLevel, esc.TriggersOverwhelmed));
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private record ResponsePayload(
        [property: JsonPropertyName("selected_goal_description")] string SelectedGoalDescription,
        [property: JsonPropertyName("selected_bad_habit")]         string SelectedBadHabit,
        [property: JsonPropertyName("danger_level")]               int    DangerLevel,
        [property: JsonPropertyName("ambient_question")]           string AmbientQuestion,
        [property: JsonPropertyName("ambient_question_answer")]    string AmbientQuestionAnswer);
}
