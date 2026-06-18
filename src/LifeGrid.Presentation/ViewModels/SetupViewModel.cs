using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Onboarding.Commands;
using LifeGrid.Application.Week;
using LifeGrid.Application.Week.Commands;
using LifeGrid.Application.Onboarding.Queries;
using LifeGrid.Domain.Onboarding;
using MediatR;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace LifeGrid.Presentation.ViewModels;

public partial class SetupViewModel(IMediator mediator, AppShellViewModel appShellViewModel) : ObservableObject
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private CancellationTokenSource? _debounceCts;
    private CancellationTokenSource? _answerDebounceCts;
    private bool _isLoading;

    [ObservableProperty] private string _goalDraft            = string.Empty;
    [ObservableProperty] private bool   _isValidating         = false;
    [ObservableProperty] private string _validationError      = string.Empty;
    [ObservableProperty] private bool   _isRefinementActive   = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEntryFlowVisible))]
    private bool _isGeneratingHabits = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEntryFlowVisible))]
    private string _infeasibilityReason = string.Empty;

    [ObservableProperty] private string _validatedGoalSummary = string.Empty;

    public bool IsEntryFlowVisible => !IsGeneratingHabits && string.IsNullOrEmpty(InfeasibilityReason);

    public ObservableCollection<RefinementItem> RefinementItems { get; } = new();

    public async Task LoadAsync()
    {
        _isLoading = true;
        try
        {
            var result = await mediator.Send(new GetOrCreateOnboardingSessionQuery());
            if (!result.IsSuccess) return;

            var session = result.Value!;
            GoalDraft = session.RawGoalDraft ?? string.Empty;

            switch (session.CurrentStep)
            {
                case OnboardingStep.Step1_RefinementQuestionsActive
                    when !string.IsNullOrWhiteSpace(session.RefinementQuestionsJson):
                    RestoreRefinementState(
                        session.RefinementQuestionsJson!,
                        session.ValidatedGoalJson,
                        session.RefinementAnswersJson);
                    break;

                case OnboardingStep.Step1_ExecutionVerified:
                    await AutoResumeHabitGenerationAsync();
                    break;
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    partial void OnGoalDraftChanged(string value)
    {
        if (!_isLoading) ScheduleAutoSave(value);
    }

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

    private void ScheduleAnswerAutoSave()
    {
        _answerDebounceCts?.Cancel();
        _answerDebounceCts = new CancellationTokenSource();
        var token = _answerDebounceCts.Token;

        var snapshot = RefinementItems
            .Select(i => new { rankOrder = i.RankOrder, answer = i.Answer })
            .ToList();
        var json = JsonSerializer.Serialize(snapshot);

        Task.Delay(500, token).ContinueWith(
            async _ => await mediator.Send(new SaveRefinementAnswersCommand(json)),
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
        _answerDebounceCts?.Cancel();

        var userAnswers = RefinementItems
            .Select(item => (item.RankOrder, item.Answer))
            .ToList();

        var finalizeResult = await mediator.Send(new FinalizeGoalCommand(userAnswers));
        if (!finalizeResult.IsSuccess)
        {
            ValidationError = finalizeResult.Error ?? "Could not finalize goal. Please try again.";
            return;
        }

        IsRefinementActive = false;
        await AutoResumeHabitGenerationAsync();
    }

    [RelayCommand]
    private void ReviseGoal()
    {
        InfeasibilityReason = string.Empty;
        ValidationError     = string.Empty;
        GoalDraft           = string.Empty;
    }

    private async Task AutoResumeHabitGenerationAsync()
    {
        IsGeneratingHabits = true;

        var habitResult = await mediator.Send(new GenerateHabitsCommand());

        IsGeneratingHabits = false;

        if (!habitResult.IsSuccess)
        {
            ValidationError = habitResult.Error ?? "Habit generation failed. Please try again.";
            return;
        }

        switch (habitResult.Value)
        {
            case HabitGenerationOutcome.Infeasible infeasible:
                var hint = infeasible.SuggestedDeadline is not null
                    ? $"\n\nSuggested deadline: {infeasible.SuggestedDeadline}"
                    : string.Empty;
                InfeasibilityReason = infeasible.RecalibrationReason + hint;
                break;

            case HabitGenerationOutcome.Complete:
                appShellViewModel.SetOnboardingComplete();
                await Shell.Current.GoToAsync("//goals");
                break;
        }
    }

    private void PopulateRefinementItems(IReadOnlyList<RefinementQuestionDto> questions)
    {
        foreach (var item in RefinementItems)
            item.PropertyChanged -= OnRefinementItemPropertyChanged;

        RefinementItems.Clear();

        foreach (var q in questions)
        {
            var item = new RefinementItem { RankOrder = q.RankOrder, Question = q.Question };
            item.PropertyChanged += OnRefinementItemPropertyChanged;
            RefinementItems.Add(item);
        }
    }

    private void OnRefinementItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RefinementItem.Answer))
            ScheduleAnswerAutoSave();
    }

    private void RestoreRefinementState(
        string  refinementQuestionsJson,
        string? validatedGoalJson,
        string? refinementAnswersJson)
    {
        try
        {
            var questions = JsonSerializer.Deserialize<List<RefinementQuestionDto>>(refinementQuestionsJson, JsonOpts);
            if (questions is null) return;

            PopulateRefinementItems(questions);

            if (!string.IsNullOrWhiteSpace(refinementAnswersJson))
                RestoreAnswers(refinementAnswersJson);

            if (!string.IsNullOrWhiteSpace(validatedGoalJson))
                TrySetGoalSummary(validatedGoalJson!);

            IsRefinementActive = true;
        }
        catch (JsonException) { }
    }

    private void RestoreAnswers(string refinementAnswersJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(refinementAnswersJson);
            var lookup = new Dictionary<int, string>();
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var rankOrder = element.GetProperty("rankOrder").GetInt32();
                var answer    = element.GetProperty("answer").GetString() ?? string.Empty;
                lookup[rankOrder] = answer;
            }

            foreach (var item in RefinementItems)
            {
                if (lookup.TryGetValue(item.RankOrder, out var ans))
                    item.Answer = ans;
            }
        }
        catch (Exception) { }
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
