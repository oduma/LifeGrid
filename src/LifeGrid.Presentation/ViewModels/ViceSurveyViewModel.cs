using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeGrid.Application.Vice;
using MediatR;
using System.Collections.ObjectModel;

namespace LifeGrid.Presentation.ViewModels;

public enum ViceSurveyState { Loading, Questions, Analyzing, Complete }

public partial class ViceSurveyViewModel(IMediator mediator, HudViewModel hud) : ObservableObject
{
    // ── State machine ──────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoadingState))]
    [NotifyPropertyChangedFor(nameof(IsQuestionsState))]
    [NotifyPropertyChangedFor(nameof(IsAnalyzingState))]
    [NotifyPropertyChangedFor(nameof(IsCompleteState))]
    private ViceSurveyState _state = ViceSurveyState.Loading;

    public bool IsLoadingState   => State == ViceSurveyState.Loading;
    public bool IsQuestionsState => State == ViceSurveyState.Questions;
    public bool IsAnalyzingState => State == ViceSurveyState.Analyzing;
    public bool IsCompleteState  => State == ViceSurveyState.Complete;

    // ── Question display ───────────────────────────────────────────────────

    [ObservableProperty] private string _progressText      = string.Empty;
    [ObservableProperty] private double _progress          = 0.0;
    [ObservableProperty] private string _questionText      = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpenEnded))]
    private bool _isMultipleChoice;

    public bool IsOpenEnded => !IsMultipleChoice;

    [ObservableProperty] private string  _freeTextAnswer   = string.Empty;
    [ObservableProperty] private bool    _isFinalQuestion;
    [ObservableProperty] private string  _actionButtonLabel = "Next";

    public ObservableCollection<OptionItemViewModel> OptionItems { get; } = new();

    // ── Completion ─────────────────────────────────────────────────────────

    [ObservableProperty] private IReadOnlyList<DetectedViceDto> _detectedVices = [];

    // ── Internal state ─────────────────────────────────────────────────────

    private IReadOnlyList<SurveyQuestionDto> _questions  = [];
    private int                              _currentIndex;
    private string?                          _selectedOption;
    private readonly List<SurveyAnswerDto>   _collectedAnswers = new();

    // ── Load ───────────────────────────────────────────────────────────────

    public async Task LoadAsync()
    {
        State = ViceSurveyState.Loading;
        _collectedAnswers.Clear();

        var result = await mediator.Send(new GetViceSurveyQuestionsQuery());
        if (!result.IsSuccess || result.Value is null || result.Value.Count == 0)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        _questions = result.Value;
        ShowQuestion(0);
        State = ViceSurveyState.Questions;
    }

    // ── Question navigation ────────────────────────────────────────────────

    private void ShowQuestion(int index)
    {
        _currentIndex   = index;
        _selectedOption = null;
        FreeTextAnswer  = string.Empty;

        var q = _questions[index];
        QuestionText      = q.QuestionText;
        IsMultipleChoice  = q.Type == "multiple_choice";
        IsFinalQuestion   = index == _questions.Count - 1;
        ActionButtonLabel = IsFinalQuestion ? "Analyze Profile" : "Next";
        ProgressText      = $"Question {index + 1} of {_questions.Count}";
        Progress          = (index + 1.0) / _questions.Count;

        OptionItems.Clear();
        if (IsMultipleChoice && q.Options is { Count: > 0 })
        {
            foreach (var opt in q.Options)
                OptionItems.Add(new OptionItemViewModel(opt, OnOptionSelected));
        }
    }

    private void OnOptionSelected(OptionItemViewModel selected)
    {
        foreach (var item in OptionItems)
            item.IsSelected = false;
        selected.IsSelected = true;
        _selectedOption     = selected.Text;
    }

    // ── Commands ───────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task NextAsync()
    {
        var answerText = IsMultipleChoice ? (_selectedOption ?? string.Empty) : FreeTextAnswer;
        _collectedAnswers.Add(new SurveyAnswerDto(_questions[_currentIndex].Id, answerText));

        if (!IsFinalQuestion)
        {
            ShowQuestion(_currentIndex + 1);
            return;
        }

        State = ViceSurveyState.Analyzing;
        var result = await mediator.Send(new SubmitViceSurveyCommand(_collectedAnswers));

        if (!result.IsSuccess)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        DetectedVices = result.Value ?? [];
        State         = ViceSurveyState.Complete;
        await hud.LoadAsync();
    }

    [RelayCommand]
    private static async Task AcceptAndReturnAsync()
        => await Shell.Current.GoToAsync("..");
}
