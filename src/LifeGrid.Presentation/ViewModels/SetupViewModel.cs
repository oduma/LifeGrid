using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Onboarding.Commands;
using LifeGrid.Application.Onboarding.Queries;
using LifeGrid.Domain.Onboarding;
using MediatR;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace LifeGrid.Presentation.ViewModels;

public partial class SetupViewModel(IMediator mediator) : ObservableObject
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private CancellationTokenSource? _debounceCts;

    [ObservableProperty] private string _goalDraft            = string.Empty;
    [ObservableProperty] private bool   _isValidating         = false;
    [ObservableProperty] private string _validationError      = string.Empty;
    [ObservableProperty] private bool   _isRefinementActive   = false;
    [ObservableProperty] private bool   _isExecutionVerified  = false;
    [ObservableProperty] private string _validatedGoalSummary = string.Empty;

    public ObservableCollection<RefinementItem> RefinementItems { get; } = new();

    public async Task LoadAsync()
    {
        var result = await mediator.Send(new GetOrCreateOnboardingSessionQuery());
        if (!result.IsSuccess) return;

        var session = result.Value!;
        GoalDraft = session.RawGoalDraft ?? string.Empty;

        switch (session.CurrentStep)
        {
            case OnboardingStep.Step1_RefinementQuestionsActive:
                if (!string.IsNullOrWhiteSpace(session.RefinementQuestionsJson))
                    RestoreRefinementState(session.RefinementQuestionsJson!, session.ValidatedGoalJson);
                break;

            case OnboardingStep.Step1_ExecutionVerified:
                IsExecutionVerified = true;
                break;
        }
    }

    partial void OnGoalDraftChanged(string value) => ScheduleAutoSave(value);

    private void ScheduleAutoSave(string draft)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        Task.Delay(500, token).ContinueWith(
            async _ => await mediator.Send(new UpdateGoalDraftCommand(draft)),
            token,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.Default);
    }

    [RelayCommand]
    private async Task CompleteStep1Async()
    {
        _debounceCts?.Cancel();
        await mediator.Send(new UpdateGoalDraftCommand(GoalDraft));

        ValidationError = string.Empty;
        IsValidating    = true;

        var result = await mediator.Send(new TriggerGoalValidationCommand());

        IsValidating = false;

        if (!result.IsSuccess)
        {
            ValidationError = result.Error ?? "Validation failed. Please try again.";
            return;
        }

        PopulateRefinementItems(result.Value!);
        IsRefinementActive = true;
    }

    [RelayCommand]
    private async Task ConfirmAndInitializeAsync()
    {
        var userAnswers = RefinementItems
            .Select(item => (item.RankOrder, item.Answer))
            .ToList();

        var result = await mediator.Send(new FinalizeGoalCommand(userAnswers));
        if (result.IsSuccess)
        {
            IsRefinementActive  = false;
            IsExecutionVerified = true;
        }
    }

    private void PopulateRefinementItems(IReadOnlyList<RefinementQuestionDto> questions)
    {
        RefinementItems.Clear();
        foreach (var q in questions)
            RefinementItems.Add(new RefinementItem { RankOrder = q.RankOrder, Question = q.Question });
    }

    private void RestoreRefinementState(string refinementQuestionsJson, string? validatedGoalJson)
    {
        try
        {
            var questions = JsonSerializer.Deserialize<List<RefinementQuestionDto>>(refinementQuestionsJson, JsonOpts);
            if (questions is null) return;

            PopulateRefinementItems(questions);

            if (!string.IsNullOrWhiteSpace(validatedGoalJson))
                TrySetGoalSummary(validatedGoalJson!);

            IsRefinementActive = true;
        }
        catch (JsonException) { }
    }

    private void TrySetGoalSummary(string validatedGoalJson)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<ValidatedGoalDto>(validatedGoalJson, JsonOpts);
            if (dto is not null)
                ValidatedGoalSummary = $"{dto.Description}  |  {dto.Duration}  |  Due {dto.DeadlineDate:yyyy-MM-dd}";
        }
        catch (JsonException) { }
    }
}
