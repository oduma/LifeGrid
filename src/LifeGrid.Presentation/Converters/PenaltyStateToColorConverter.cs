using System.Globalization;

namespace LifeGrid.Presentation.Converters;

public sealed class PenaltyStateToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value as string) switch
        {
            "Clean"          => Color.FromArgb("#58585a"),  // OnSurface    — standard
            "Level1Warning"  => Color.FromArgb("#a20ba0"),  // On-Secondary — caution
            "ProbationWeek2" => Color.FromArgb("#FF1B77"),  // Error        — severe
            "ReckoningWeek3" => Color.FromArgb("#FF1B77"),  // Error        — critical
            _                => Color.FromArgb("#58585a")
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
