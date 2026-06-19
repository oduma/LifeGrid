using System.Globalization;

namespace LifeGrid.Presentation.Converters;

public sealed class DangerLevelToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int level && level >= 4)
            return Microsoft.Maui.Controls.Application.Current?.Resources["Error"] as Color;

        return Microsoft.Maui.Controls.Application.Current?.Resources["Primary"] as Color;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
