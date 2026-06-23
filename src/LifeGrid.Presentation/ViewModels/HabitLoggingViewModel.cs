using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeGrid.Application.HabitLogging;
using MediatR;

namespace LifeGrid.Presentation.ViewModels;

public partial class HabitLoggingViewModel(IMediator mediator)
    : ObservableObject, IQueryAttributable
{
    [ObservableProperty] private Guid    _habitId;
    [ObservableProperty] private string  _habitName        = string.Empty;
    [ObservableProperty] private string  _habitDescription = string.Empty;
    [ObservableProperty] private string  _targetText       = string.Empty;
    [ObservableProperty] private string  _measurementUnit  = string.Empty;
    [ObservableProperty] private string  _goalDescription  = string.Empty;
    [ObservableProperty] private string  _weekLabel        = string.Empty;
    [ObservableProperty] private string  _actualValue      = string.Empty;
    [ObservableProperty] private string? _proofText;
    [ObservableProperty] private string? _proofImageUrl;
    [ObservableProperty] private string  _errorMessage     = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LogProgressCommand))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("habitId",          out var hid) && hid is Guid g)
            HabitId = g;
        if (query.TryGetValue("habitName",         out var hn))  HabitName        = hn.ToString()!;
        if (query.TryGetValue("habitDescription",  out var hd))  HabitDescription = hd.ToString()!;
        if (query.TryGetValue("targetText",        out var tt))  TargetText       = tt.ToString()!;
        if (query.TryGetValue("measurementUnit",   out var mu))  MeasurementUnit  = mu.ToString()!;
        if (query.TryGetValue("goalDescription",   out var gd))  GoalDescription  = gd.ToString()!;
        if (query.TryGetValue("weekLabel",         out var wl))  WeekLabel        = wl.ToString()!;

        ActualValue   = string.Empty;
        ProofText     = null;
        ProofImageUrl = null;
        ErrorMessage  = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task LogProgressAsync()
    {
        ErrorMessage = string.Empty;

        if (!double.TryParse(ActualValue, out var value) || value <= 0)
        {
            ErrorMessage = "Please enter a value greater than zero.";
            return;
        }

        IsBusy = true;
        var result = await mediator.Send(new LogHabitProgressCommand(
            HabitId,
            value,
            MeasurementUnit,
            string.IsNullOrWhiteSpace(ProofText)     ? null : ProofText,
            ProofImageUrl));
        IsBusy = false;

        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error ?? "Failed to log progress.";
            return;
        }

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task PickProofImageAsync()
    {
        var picked = await FilePicker.Default.PickAsync(new PickOptions
        {
            FileTypes    = FilePickerFileType.Images,
            PickerTitle  = "Select proof image"
        });
        if (picked is not null)
            ProofImageUrl = picked.FullPath;
    }
}
