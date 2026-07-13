using System.Text.Json;
using System.Text.Json.Serialization;
using LifeGrid.Application.Common;
using LifeGrid.Application.Gamification;
using LifeGrid.Application.Goal;
using LifeGrid.Application.UserProfile;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Common;
using LifeGrid.Domain.Week;
using MediatR;
using GoalEntity          = LifeGrid.Domain.Goal.Goal;
using LinkedBadHabitEntity = LifeGrid.Domain.Goal.LinkedBadHabit;
using ViceCheckAuditEntity = LifeGrid.Domain.ViceCheck.ViceCheckAudit;
using WeekGoalEntity       = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Application.ViceCheck;

public sealed class InitiateViceCheckCommandHandler(
    IWeekRepository          weekRepository,
    IGoalRepository          goalRepository,
    IUserProfileRepository   userProfileRepository,
    IViceCheckAuditRepository auditRepository,
    IGeminiViceCheckService  geminiViceCheckService,
    IRandomProvider          randomProvider,
    IDateTimeProvider        dateTimeProvider,
    IUnitOfWork              unitOfWork,
    IEconomyStateBroadcaster broadcaster)
    : IRequestHandler<InitiateViceCheckCommand, Result<InitiateViceCheckResult>>
{
    public async Task<Result<InitiateViceCheckResult>> Handle(
        InitiateViceCheckCommand request, CancellationToken cancellationToken)
    {
        var week = await weekRepository.GetByIdAsync(request.WeekId, cancellationToken);
        if (week is null)
            return Result<InitiateViceCheckResult>.Failure("week_not_found");

        if (week.Status != WeekStatus.Closed)
            return Result<InitiateViceCheckResult>.Failure("week_not_closed");

        var profile = await userProfileRepository.GetSingleAsync(cancellationToken);
        if (profile is null || !profile.IsViceSurveyCompleted)
            return Result<InitiateViceCheckResult>.Failure("vice_survey_not_completed");

        var alreadyAudited = await auditRepository.HasAuditForWeekAsync(week.WeekId, cancellationToken);
        if (alreadyAudited)
            return Result<InitiateViceCheckResult>.Failure("already_audited");

        var cutoff = week.StartDate.AddDays(6).AddHours(72);
        if (dateTimeProvider.UtcNow > cutoff)
            return Result<InitiateViceCheckResult>.Failure("window_expired");

        var goalIds = week.WeekGoals.Select(wg => wg.GoalId).Distinct().ToList();
        var goals   = await goalRepository.GetByIdsAsync(goalIds, cancellationToken);
        var goalsById = goals.ToDictionary(g => g.GoalId);

        var triples = new List<(WeekGoalEntity WeekGoal, GoalEntity Goal, LinkedBadHabitEntity BadHabit)>();
        foreach (var weekGoal in week.WeekGoals)
        {
            if (!goalsById.TryGetValue(weekGoal.GoalId, out var goal)) continue;
            foreach (var badHabit in goal.LinkedBadHabits)
                triples.Add((weekGoal, goal, badHabit));
        }

        if (triples.Count == 0)
            return Result<InitiateViceCheckResult>.Failure("no_bad_habits_available");

        var selected = triples[randomProvider.Next(triples.Count)];

        var previousQuestions = await auditRepository.GetPreviousQuestionsForBadHabitAsync(
            selected.BadHabit.BadHabitId, cancellationToken);

        var payload = new List<PayloadGoal>
        {
            new(selected.Goal.Description, new List<PayloadBadHabit>
            {
                new(selected.BadHabit.Description, selected.BadHabit.DangerLevel, previousQuestions)
            })
        };
        var payloadJson = JsonSerializer.Serialize(payload, JsonOpts);

        var aiResult = await geminiViceCheckService.GenerateQuestionAsync(payloadJson, cancellationToken);
        if (!aiResult.IsSuccess)
            return Result<InitiateViceCheckResult>.Failure(aiResult.Error!);

        profile.ApplyXpAndLevelProgression(20);

        var audit = ViceCheckAuditEntity.Create(
            week.WeekId,
            selected.WeekGoal.WeekGoalId,
            selected.BadHabit.BadHabitId,
            selected.Goal.Description,
            selected.BadHabit.Description,
            selected.BadHabit.DangerLevel,
            aiResult.Value!.Question,
            dateTimeProvider.UtcNow);

        await auditRepository.AddAsync(audit, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        broadcaster.BroadcastEconomy(profile.Economy.CurrentSp, profile.Economy.ShieldsAvailable);

        return Result<InitiateViceCheckResult>.Success(new InitiateViceCheckResult(audit.AuditId, audit.Question));
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private record PayloadGoal(
        string Description,
        [property: JsonPropertyName("bad_habits")] IReadOnlyList<PayloadBadHabit> BadHabits);

    private record PayloadBadHabit(
        string Description,
        [property: JsonPropertyName("danger_level")]      int                      DangerLevel,
        [property: JsonPropertyName("previous_questions")] IReadOnlyList<string>   PreviousQuestions);
}
