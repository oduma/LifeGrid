using System.Globalization;

namespace LifeGrid.Presentation.Converters;

public sealed class StatusToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value as string) switch
        {
            "Active"      => Color.FromArgb("#35f8db"),  // Primary       — "active states"
            "Overwhelmed" => Color.FromArgb("#a20ba0"),  // On-Secondary  — warning/stress
            "Abandoned"   => Color.FromArgb("#FF1B77"),  // Error         — "destructive actions"
            "Completed"   => Color.FromArgb("#58585a"),  // On-Surface    — neutral/closed
            _             => Color.FromArgb("#58585a")   // fallback
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
